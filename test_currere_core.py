"""
╔══════════════════════════════════════════════════════════════════╗
║  Currere Core — Büyük Dörtlü Entegrasyon Test Paketi           ║
║  ═══════════════════════════════════════════════════════         ║
║  Adım 1: Docker Dosya Okuma (Volume Mount + Symlink)           ║
║  Adım 2: Stateful Kernel (Hücreler Arası Hafıza)              ║
║  Adım 3: Snapshot Transaction Güvenliği                        ║
║  Adım 4: SignalR Sağlık Kontrolü                               ║
║                                                                  ║
║  Çalıştırma:                                                     ║
║    pip install pytest requests                                   ║
║    pytest test_currere_core.py -v --tb=short                    ║
╚══════════════════════════════════════════════════════════════════╝
"""

import pytest
import requests
import time
import json

# ══════════════════════════════════════════════════════════════════
# KONFİGÜRASYON — Ortamınıza göre düzenleyin
# ══════════════════════════════════════════════════════════════════
BASE_URL = "http://localhost:5279/api"
SIGNALR_URL = "http://localhost:5279/syncHub"

# Test kullanıcısı (veritabanında mevcut olmalı)
TEST_EMAIL = "test@currere.dev"
TEST_PASSWORD = "Test123!"

# Test workspace ID (mevcut bir workspace ID'si)
# Eğer yoksa testler yeni bir workspace yaratır
TEST_WORKSPACE_ID = None  # Otomatik oluşturulacak


# ══════════════════════════════════════════════════════════════════
# FIXTURE: JWT Token ile Kimlik Doğrulama
# ══════════════════════════════════════════════════════════════════
@pytest.fixture(scope="session")
def auth_headers():
    """
    Test kullanıcısıyla giriş yapıp JWT token alır.
    Eğer kullanıcı yoksa önce kaydeder.
    """
    # Önce giriş yapmayı dene
    login_res = requests.post(f"{BASE_URL}/auth/login", json={
        "email": TEST_EMAIL,
        "password": TEST_PASSWORD
    })

    if login_res.status_code == 401:
        # Kullanıcı yok — kayıt ol
        register_res = requests.post(f"{BASE_URL}/auth/register", json={
            "firstName": "Test",
            "lastName": "Otomasyon",
            "email": TEST_EMAIL,
            "password": TEST_PASSWORD
        })
        assert register_res.status_code == 200, f"Kayıt başarısız: {register_res.text}"

        # Şimdi giriş yap
        login_res = requests.post(f"{BASE_URL}/auth/login", json={
            "email": TEST_EMAIL,
            "password": TEST_PASSWORD
        })

    assert login_res.status_code == 200, f"Giriş başarısız: {login_res.text}"
    token = login_res.json().get("token")
    assert token, "JWT token alınamadı"

    return {"Authorization": f"Bearer {token}"}


@pytest.fixture(scope="session")
def workspace_id(auth_headers):
    """
    Test için yeni bir workspace oluşturur.
    Testler bittikten sonra silinebilir.
    """
    global TEST_WORKSPACE_ID
    if TEST_WORKSPACE_ID:
        return TEST_WORKSPACE_ID

    res = requests.post(f"{BASE_URL}/workspace", json={
        "title": "Test Otomasyon Sandbox",
        "format": 1,  # Python
        "runtime": 1  # CPU
    }, headers=auth_headers)

    assert res.status_code == 200 or res.status_code == 201, f"Workspace oluşturulamadı: {res.text}"
    ws_id = res.json().get("id") or res.json().get("workspaceId")
    assert ws_id, f"Workspace ID alınamadı: {res.json()}"

    TEST_WORKSPACE_ID = ws_id
    print(f"\n✅ Test Workspace oluşturuldu: ID={ws_id}")
    return ws_id


# ══════════════════════════════════════════════════════════════════
# ADIM 2: STATEFUL KERNEL TESTİ
# ══════════════════════════════════════════════════════════════════
class TestStatefulKernel:
    """
    Jupyter Stateful Kernel — Hücreler arası değişken persistansı.
    Hücre 1'de tanımlanan değişken, Hücre 2'de kullanılabilmeli.
    """

    def test_kernel_variable_persistence(self, auth_headers, workspace_id):
        """
        Senaryo:
        1. Hücre 1: x = 'Otomasyon Başarılı' tanımla
        2. Hücre 2: print(x) çalıştır
        3. Çıktıda 'Otomasyon Başarılı' mesajı olmalı
        """
        # Kernel'ı temiz başlat
        restart_res = requests.post(
            f"{BASE_URL}/kernel/{workspace_id}/restart",
            headers=auth_headers
        )
        print(f"Kernel restart: {restart_res.status_code}")

        # Hücre 1: Değişken tanımla
        cell1_res = requests.post(
            f"{BASE_URL}/kernel/{workspace_id}/execute",
            json={"code": "x = 'Otomasyon Başarılı'"},
            headers=auth_headers,
            timeout=60
        )
        assert cell1_res.status_code == 200, f"Hücre 1 başarısız: {cell1_res.text}"
        cell1_data = cell1_res.json()
        assert cell1_data["success"], f"Hücre 1 hata: {cell1_data}"

        # Hücre 2: Değişkeni yazdır (hafızadan gelmeli)
        cell2_res = requests.post(
            f"{BASE_URL}/kernel/{workspace_id}/execute",
            json={"code": "print(x)"},
            headers=auth_headers,
            timeout=60
        )
        assert cell2_res.status_code == 200, f"Hücre 2 başarısız: {cell2_res.text}"
        cell2_data = cell2_res.json()

        assert cell2_data["success"], f"Hücre 2 hata: {cell2_data}"
        assert "Otomasyon Başarılı" in cell2_data["output"], \
            f"Değişken hafızada YOK! Beklenen: 'Otomasyon Başarılı', Alınan: '{cell2_data['output']}'"

        print(f"✅ Stateful Kernel: Değişken hücreler arası yaşıyor → '{cell2_data['output'].strip()}'")

    def test_kernel_status(self, auth_headers, workspace_id):
        """Kernel status endpoint'i çalışmalı."""
        res = requests.get(
            f"{BASE_URL}/kernel/{workspace_id}/status",
            headers=auth_headers
        )
        assert res.status_code == 200
        data = res.json()
        assert "isAlive" in data
        print(f"✅ Kernel Status: isAlive={data['isAlive']}")

    def test_kernel_restart_clears_state(self, auth_headers, workspace_id):
        """Restart sonrası değişkenler sıfırlanmalı."""
        # Değişken tanımla
        requests.post(
            f"{BASE_URL}/kernel/{workspace_id}/execute",
            json={"code": "restart_test_var = 42"},
            headers=auth_headers,
            timeout=60
        )

        # Restart
        restart_res = requests.post(
            f"{BASE_URL}/kernel/{workspace_id}/restart",
            headers=auth_headers
        )
        assert restart_res.status_code == 200

        # Eski değişken artık olmamalı
        check_res = requests.post(
            f"{BASE_URL}/kernel/{workspace_id}/execute",
            json={"code": "print(restart_test_var)"},
            headers=auth_headers,
            timeout=60
        )
        check_data = check_res.json()
        assert not check_data["success"], "Restart sonrası değişken hâlâ yaşıyor!"
        print(f"✅ Kernel Restart: Hafıza başarıyla sıfırlandı")


# ══════════════════════════════════════════════════════════════════
# ADIM 1: DOCKER DOSYA OKUMA TESTİ
# ══════════════════════════════════════════════════════════════════
class TestDockerFileAccess:
    """
    Docker Volume Mount — Workspace dosyalarının konteyner içinden okunması.
    Symlink köprüsü GUID prefixli dosyaları orijinal isimle erişilebilir yapmalı.
    """

    def test_file_read_in_kernel(self, auth_headers, workspace_id):
        """
        Senaryo:
        1. Workspace'e test_data.csv yükle
        2. Kernel üzerinden dosyayı oku
        3. İçeriğin doğru geldiğini doğrula
        """
        csv_content = "name,age,city\nAli,25,Istanbul\nVeli,30,Ankara\n"

        # 1. Dosyayı workspace'e yükle
        files = {"file": ("test_data.csv", csv_content, "text/csv")}
        upload_res = requests.post(
            f"{BASE_URL}/workspace/{workspace_id}/file/upload",
            files=files,
            headers=auth_headers
        )
        assert upload_res.status_code == 200, f"Dosya yükleme başarısız: {upload_res.text}"
        print(f"  📁 test_data.csv yüklendi")

        # 2. Kernel'ı restart et (temiz symlink)
        requests.post(f"{BASE_URL}/kernel/{workspace_id}/restart", headers=auth_headers)
        time.sleep(2)  # Kernel hazır olsun

        # 3. Kernel üzerinden dosyayı oku
        read_res = requests.post(
            f"{BASE_URL}/kernel/{workspace_id}/execute",
            json={"code": "content = open('test_data.csv').read()\nprint(content)"},
            headers=auth_headers,
            timeout=60
        )
        assert read_res.status_code == 200, f"Kernel execute başarısız: {read_res.text}"
        read_data = read_res.json()

        assert read_data["success"], f"Dosya okunamadı: {read_data}"
        assert "Ali" in read_data["output"], \
            f"Dosya içeriği yanlış! Beklenen: CSV verisi, Alınan: '{read_data['output'][:100]}'"

        print(f"✅ Docker Dosya Okuma: Symlink köprüsü çalışıyor → CSV başarıyla okundu")

    def test_pandas_read_csv(self, auth_headers, workspace_id):
        """pandas.read_csv() ile dosya okunabilmeli."""
        code = (
            "import pandas as pd\n"
            "df = pd.read_csv('test_data.csv')\n"
            "print(f'Satır: {len(df)}, Sütun: {len(df.columns)}')\n"
            "print(df.to_string(index=False))"
        )
        res = requests.post(
            f"{BASE_URL}/kernel/{workspace_id}/execute",
            json={"code": code},
            headers=auth_headers,
            timeout=60
        )
        data = res.json()
        assert data["success"], f"pandas read_csv başarısız: {data}"
        assert "Satır: 2" in data["output"], f"Satır sayısı yanlış: {data['output']}"
        print(f"✅ Pandas Entegrasyonu: DataFrame başarıyla oluşturuldu → {data['output'].split(chr(10))[0]}")


# ══════════════════════════════════════════════════════════════════
# ADIM 3: SNAPSHOT GÜVENLİK TESTİ
# ══════════════════════════════════════════════════════════════════
class TestSnapshotSecurity:
    """
    Zaman Makinesi — 0-byte yedek koruması ve metadata doğrulaması.
    """

    def test_empty_workspace_snapshot_blocked(self, auth_headers):
        """
        Senaryo:
        Boş bir workspace için snapshot isteği atıldığında
        sistem 400 Bad Request dönmeli (0-byte yedek oluşturmamalı).
        """
        # Yeni boş workspace oluştur
        ws_res = requests.post(f"{BASE_URL}/workspace", json={
            "title": "Bos Test Workspace",
            "format": 1,
            "runtime": 1
        }, headers=auth_headers)

        assert ws_res.status_code in [200, 201], f"Workspace oluşturulamadı: {ws_res.text}"
        empty_ws_id = ws_res.json().get("id") or ws_res.json().get("workspaceId")

        # Boş workspace için snapshot dene
        snap_res = requests.post(
            f"{BASE_URL}/workspace/{empty_ws_id}/snapshot",
            json={"label": "Boş Workspace Testi", "description": "Bu başarısız olmalı"},
            headers=auth_headers
        )

        assert snap_res.status_code == 400, \
            f"Boş workspace'te snapshot BAŞARILI oldu (olmamalıydı)! Status: {snap_res.status_code}, Body: {snap_res.text}"

        error_msg = snap_res.json().get("error", "")
        assert "boş" in error_msg.lower() or "bulunamadı" in error_msg.lower() or "dosya" in error_msg.lower(), \
            f"Hata mesajı anlamlı değil: '{error_msg}'"

        print(f"✅ Snapshot Güvenlik: Boş workspace koruması çalışıyor → '{error_msg[:60]}'")

    def test_snapshot_with_label_and_metadata(self, auth_headers, workspace_id):
        """
        Senaryo:
        Dosya olan bir workspace'te snapshot alınmalı ve
        dönen yanıtta sizeKB, fileCount ve snapshotId olmalı.
        """
        # Önce workspace'e bir kod kaydet (dosya olsun)
        requests.put(
            f"{BASE_URL}/workspace/{workspace_id}/code",
            json={"code": "print('Snapshot Test')"},
            headers=auth_headers
        )

        snap_res = requests.post(
            f"{BASE_URL}/workspace/{workspace_id}/snapshot",
            json={"label": "Otomasyon Test Yedeği", "description": "Pytest tarafından oluşturuldu"},
            headers=auth_headers
        )
        assert snap_res.status_code == 200, f"Snapshot başarısız: {snap_res.text}"

        data = snap_res.json()
        assert "snapshotId" in data, f"snapshotId eksik: {data}"
        assert data.get("message"), f"Başarı mesajı eksik: {data}"

        print(f"✅ Snapshot Oluşturma: ID={data['snapshotId']}, "
              f"Size={data.get('sizeKB', '?')}KB, Files={data.get('fileCount', '?')}")

    def test_snapshot_history(self, auth_headers, workspace_id):
        """Snapshot geçmişi listelenebilmeli."""
        res = requests.get(
            f"{BASE_URL}/workspace/{workspace_id}/snapshot",
            headers=auth_headers
        )
        assert res.status_code == 200
        snapshots = res.json()
        assert isinstance(snapshots, list)
        assert len(snapshots) > 0, "Geçmişte hiç snapshot yok"
        print(f"✅ Snapshot Geçmişi: {len(snapshots)} yedek bulundu")


# ══════════════════════════════════════════════════════════════════
# ADIM 4: SIGNALR SAĞLIK KONTROLÜ
# ══════════════════════════════════════════════════════════════════
class TestSignalRHealth:
    """
    SignalR Hub — Ulaşılabilirlik ve CORS testi.
    Python'dan tam WebSocket bağlantısı kurmak yerine
    negotiate endpoint'ini test ediyoruz.
    """

    def test_signalr_negotiate_reachable(self):
        """
        SignalR negotiate endpoint'i erişilebilir olmalı.
        200 OK veya CORS preflight yanıtı almalıyız.
        """
        # SignalR negotiate endpoint
        negotiate_url = f"{SIGNALR_URL}/negotiate?negotiateVersion=1"

        res = requests.post(negotiate_url, headers={
            "Origin": "http://localhost:3000",
            "Content-Type": "text/plain;charset=UTF-8"
        })

        # 200 (başarılı negotiate) veya 401 (auth gerekli ama erişilebilir)
        assert res.status_code in [200, 401], \
            f"SignalR unreachable! Status: {res.status_code}, Body: {res.text[:200]}"

        print(f"✅ SignalR Negotiate: Status={res.status_code} — Hub erişilebilir")

    def test_signalr_cors_preflight(self):
        """
        CORS preflight (OPTIONS) isteği başarılı olmalı.
        Tarayıcı bu isteği otomatik atar.
        """
        res = requests.options(SIGNALR_URL, headers={
            "Origin": "http://localhost:3000",
            "Access-Control-Request-Method": "POST",
            "Access-Control-Request-Headers": "content-type,authorization"
        })

        # 200 veya 204 (preflight başarılı)
        assert res.status_code in [200, 204], \
            f"CORS preflight başarısız! Status: {res.status_code}"

        # CORS headerları kontrol et
        cors_origin = res.headers.get("Access-Control-Allow-Origin", "")
        assert cors_origin, f"Access-Control-Allow-Origin header eksik! Headers: {dict(res.headers)}"

        print(f"✅ SignalR CORS: Preflight OK, Allow-Origin='{cors_origin}'")


# ══════════════════════════════════════════════════════════════════
# BONUS: API SAĞLIK KONTROLÜ
# ══════════════════════════════════════════════════════════════════
class TestAPIHealth:
    """Temel API sağlık kontrolleri."""

    def test_api_reachable(self):
        """Backend ayakta mı?"""
        try:
            res = requests.get(f"{BASE_URL}/auth/login", timeout=5)
            # 405 Method Not Allowed bile olsa, API ayakta demektir
            assert res.status_code != 0
            print(f"✅ API Sağlık: Backend ayakta (Status={res.status_code})")
        except requests.ConnectionError:
            pytest.fail("❌ Backend'e bağlanılamıyor! 'dotnet run' çalışıyor mu?")

    def test_auth_flow(self, auth_headers):
        """JWT token alınabilmeli."""
        assert "Authorization" in auth_headers
        assert auth_headers["Authorization"].startswith("Bearer ")
        print(f"✅ Auth: JWT token alındı ({len(auth_headers['Authorization'])} karakter)")


# ══════════════════════════════════════════════════════════════════
# ANA GİRİŞ NOKTASI
# ══════════════════════════════════════════════════════════════════
if __name__ == "__main__":
    print("\n" + "═" * 60)
    print("  🚀 CURRERE CORE — Büyük Dörtlü Entegrasyon Testleri")
    print("═" * 60 + "\n")
    pytest.main([__file__, "-v", "--tb=short", "-s"])
