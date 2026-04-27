"""
╔══════════════════════════════════════════════════════════════════╗
║  Currere — Faz 2: The Fortress — Red Team Sızma Testi          ║
║  ═══════════════════════════════════════════════════════         ║
║  8 Saldırı Vektörü × 7 Güvenlik Katmanı                       ║
║                                                                  ║
║  Çalıştırma:                                                     ║
║    pip install pytest requests websockets                        ║
║    pytest test_security_fortress.py -v --tb=short -s            ║
╚══════════════════════════════════════════════════════════════════╝
"""

import pytest
import requests
import asyncio
import time
import json

# ══════════════════════════════════════════════════════════════════
# KONFİGÜRASYON
# ══════════════════════════════════════════════════════════════════
BASE_URL = "http://localhost:5279/api"
SIGNALR_NEGOTIATE_URL = "http://localhost:5279/syncHub/negotiate?negotiateVersion=1"
SIGNALR_WS_URL = "ws://localhost:5279/syncHub"

TEST_EMAIL = "redteam@currere.dev"
TEST_PASSWORD = "RedTeam2026!"

# Rate limit bypass — appsettings.json içindeki TestSettings:RateLimitBypassSecret ile eşleşmeli
TEST_SECRET = "Currere_RedTeam_2026_BypassKey!"

TEST_WORKSPACE_ID = None


# ══════════════════════════════════════════════════════════════════
# FIXTURE'LAR
# ══════════════════════════════════════════════════════════════════
@pytest.fixture(scope="session")
def auth_token():
    """JWT token al (kullanıcı yoksa kaydet)."""
    login_res = requests.post(f"{BASE_URL}/auth/login", json={
        "email": TEST_EMAIL, "password": TEST_PASSWORD
    })
    if login_res.status_code == 401:
        requests.post(f"{BASE_URL}/auth/register", json={
            "firstName": "Red", "lastName": "Team",
            "email": TEST_EMAIL, "password": TEST_PASSWORD
        })
        login_res = requests.post(f"{BASE_URL}/auth/login", json={
            "email": TEST_EMAIL, "password": TEST_PASSWORD
        })
    assert login_res.status_code == 200, f"Login başarısız: {login_res.text}"
    token = login_res.json().get("token")
    assert token, "Token alınamadı"
    return token


@pytest.fixture(scope="session")
def auth_headers(auth_token):
    return {
        "Authorization": f"Bearer {auth_token}",
        "X-Currere-Test-Secret": TEST_SECRET
    }


@pytest.fixture(scope="session")
def workspace_id(auth_headers):
    global TEST_WORKSPACE_ID
    if TEST_WORKSPACE_ID:
        return TEST_WORKSPACE_ID
    res = requests.post(f"{BASE_URL}/workspace", json={
        "title": "RedTeam Fortress Test", "format": 1, "runtime": 1
    }, headers=auth_headers)
    assert res.status_code in [200, 201], f"Workspace oluşturulamadı: {res.text}"
    ws_id = res.json().get("id") or res.json().get("workspaceId")
    TEST_WORKSPACE_ID = ws_id
    print(f"\n🎯 Red Team Workspace: ID={ws_id}")

    # Testler başlamadan önce kernel'ı sıfırla (temiz ortam)
    print(f"🧹 Kernel sıfırlanıyor...")
    requests.post(f"{BASE_URL}/kernel/{ws_id}/restart", headers=auth_headers)
    time.sleep(3)

    return ws_id


def kernel_exec(auth_headers, workspace_id, code, timeout=60):
    """Kernel üzerinde kod çalıştır ve sonucu döndür (test secret ile)."""
    headers = {**auth_headers, "X-Currere-Test-Secret": TEST_SECRET}
    res = requests.post(
        f"{BASE_URL}/kernel/{workspace_id}/execute",
        json={"code": code}, headers=headers, timeout=timeout
    )
    return res


# ══════════════════════════════════════════════════════════════════
# BÖLÜM 1: API VE AĞ GÜVENLİĞİ
# ══════════════════════════════════════════════════════════════════

class TestPathTraversal:
    """Saldırı 1: Path Traversal (Adım 7) — Dizin atlama girişimleri."""

    PAYLOADS = [
        "../../../etc/passwd",
        "....//....//....//etc/passwd",
        "..\\..\\..\\appsettings.json",
        "%2e%2e%2f%2e%2e%2fappsettings.json",
        "....//....//appsettings.json",
    ]

    @pytest.mark.parametrize("malicious_name", PAYLOADS)
    def test_path_traversal_read_blocked(self, auth_headers, workspace_id, malicious_name):
        """GET /file/{malicious}/raw → 400 veya 403 olmalı, kesinlikle 200 OLMAMALI."""
        res = requests.get(
            f"{BASE_URL}/workspace/{workspace_id}/file/{malicious_name}/raw",
            headers=auth_headers
        )
        assert res.status_code != 200, \
            f"🔴 PATH TRAVERSAL BAŞARILI! Payload: '{malicious_name}' → 200 döndü!"
        assert res.status_code in [400, 403, 404, 500], \
            f"Beklenmeyen status: {res.status_code} — Payload: '{malicious_name}'"
        print(f"  ✅ Engellendi [{res.status_code}]: {malicious_name}")

    def test_path_traversal_create_blocked(self, auth_headers, workspace_id):
        """POST /file/create ile traversal dosya adı → Engellenmeli."""
        res = requests.post(
            f"{BASE_URL}/workspace/{workspace_id}/file/create",
            json={"fileName": "../../../etc/evil.py"},
            headers=auth_headers
        )
        assert res.status_code != 200, "🔴 Traversal ile dosya OLUŞTURULDU!"
        print(f"  ✅ Dosya oluşturma traversal engellendi [{res.status_code}]")


class TestRateLimitDDoS:
    """Saldırı 2: Rate Limit DDoS Testi (Adım 9) — Hız sınırı aşımı."""

    def test_kernel_rate_limit_enforced(self, auth_headers, workspace_id):
        """
        Kernel Execute ucuna eş zamanlı 25 istek fırlat.
        KernelExecLimit = 20/dakika → 21. istekten sonra 429 beklenir.
        """
        from concurrent.futures import ThreadPoolExecutor, as_completed

        # Önce kernel'ı hazırla
        requests.post(f"{BASE_URL}/kernel/{workspace_id}/restart", headers=auth_headers)
        time.sleep(3)

        def fire_request(i):
            # Rate limit testinde bypass header'ı KASITLI olarak GÖNDERMİYORUZ
            no_bypass_headers = {k: v for k, v in auth_headers.items() if k != "X-Currere-Test-Secret"}
            try:
                res = requests.post(
                    f"{BASE_URL}/kernel/{workspace_id}/execute",
                    json={"code": f"x_{i} = {i}"},
                    headers=no_bypass_headers, timeout=10
                )
                return res.status_code
            except requests.Timeout:
                return "timeout"
            except Exception:
                return "error"

        # 25 isteği eş zamanlı fırlat
        results = []
        with ThreadPoolExecutor(max_workers=25) as executor:
            futures = [executor.submit(fire_request, i) for i in range(25)]
            for future in as_completed(futures):
                results.append(future.result())

        ok_count = results.count(200)
        blocked_count = results.count(429)
        timeout_count = results.count("timeout")

        print(f"  📊 Sonuç: {ok_count} başarılı, {blocked_count} engellendi, {timeout_count} timeout (toplam {len(results)})")
        assert blocked_count > 0, \
            f"🔴 Rate limit DEVREYE GİRMEDİ! Sonuçlar: {results}"
        print(f"  ✅ Rate Limiting çalışıyor — {blocked_count} istek 429 ile reddedildi")


class TestSignalRUnauthorized:
    """Saldırı 3: SignalR Kaçak Yolcu (Adım 11) — Tokensiz bağlantı."""

    def test_signalr_negotiate_requires_auth(self):
        """Token olmadan negotiate → 401."""
        res = requests.post(SIGNALR_NEGOTIATE_URL)
        assert res.status_code == 401, \
            f"🔴 SignalR negotiate tokensiz GEÇTİ! Status: {res.status_code}"
        print(f"  ✅ SignalR negotiate tokensiz reddedildi [401]")

    def test_signalr_negotiate_with_fake_token(self):
        """Sahte token ile negotiate → 401."""
        res = requests.post(
            SIGNALR_NEGOTIATE_URL,
            headers={"Authorization": "Bearer FAKE_TOKEN_12345"}
        )
        assert res.status_code == 401, \
            f"🔴 Sahte token ile SignalR GEÇTİ! Status: {res.status_code}"
        print(f"  ✅ Sahte token reddedildi [401]")

    def test_signalr_negotiate_with_valid_token(self, auth_token):
        """Geçerli token ile negotiate → 200."""
        res = requests.post(
            SIGNALR_NEGOTIATE_URL,
            headers={"Authorization": f"Bearer {auth_token}"}
        )
        assert res.status_code == 200, \
            f"Geçerli token reddedildi! Status: {res.status_code}, Body: {res.text[:200]}"
        print(f"  ✅ Geçerli token ile negotiate başarılı [200]")

    def test_websocket_upgrade_without_token_rejected(self):
        """WebSocket upgrade tokensiz yapılmamalı (HTTP fallback testi)."""
        try:
            res = requests.get(
                "http://localhost:5279/syncHub",
                headers={
                    "Connection": "Upgrade",
                    "Upgrade": "websocket",
                    "Sec-WebSocket-Version": "13",
                    "Sec-WebSocket-Key": "dGVzdA==",
                },
                timeout=5,
                allow_redirects=False
            )
            # 401 veya bağlantı reddi bekleniyor
            assert res.status_code in [401, 403], \
                f"Beklenmeyen status: {res.status_code}"
            print(f"  ✅ WebSocket upgrade tokensiz reddedildi [{res.status_code}]")
        except requests.ConnectionError:
            print(f"  ✅ WebSocket bağlantısı reddedildi (ConnectionError)")
        except requests.Timeout:
            print(f"  ✅ WebSocket bağlantısı zaman aşımına uğradı")


# ══════════════════════════════════════════════════════════════════
# BÖLÜM 2: DOCKER İZOLASYON SALDIRILARI
# ══════════════════════════════════════════════════════════════════

class TestNonRootContainer:
    """Saldırı 4: Non-Root (Adım 6) — Konteyner root ile çalışmamalı."""

    def test_container_runs_as_non_root(self, auth_headers, workspace_id):
        """os.getuid() → 1000 (currere), kesinlikle 0 (root) OLMAMALI."""
        res = kernel_exec(auth_headers, workspace_id, "import os; print(os.getuid())")
        assert res.status_code == 200
        data = res.json()
        assert data["success"], f"Kod çalışmadı: {data}"

        uid = data["output"].strip()
        assert uid != "0", "🔴 KONTEYNER ROOT OLARAK ÇALIŞIYOR! UID=0"
        assert uid == "1000", f"Beklenmeyen UID: {uid} (beklenen: 1000)"
        print(f"  ✅ Non-Root doğrulandı: UID={uid} (currere)")


class TestReadOnlyFilesystem:
    """Saldırı 5: Read-Only FS (Adım 6) — Dosya sistemi yazılamaz."""

    def test_write_to_root_fs_blocked(self, auth_headers, workspace_id):
        """Konteyner dosya sistemine yazma → OSError."""
        code = "open('/etc/hacked.txt', 'w').write('pwned')"
        res = kernel_exec(auth_headers, workspace_id, code)
        assert res.status_code == 200
        data = res.json()

        assert not data["success"], "🔴 Read-only FS'e YAZILDI!"
        error_msg = (data.get("error", "") + data.get("output", "")).lower()
        assert any(k in error_msg for k in ["read-only", "readonly", "permission", "oserror"]), \
            f"Beklenmeyen hata: {data}"
        print(f"  ✅ Read-only FS koruması çalışıyor: {data.get('errorType', 'OSError')}")

    def test_write_to_output_allowed(self, auth_headers, workspace_id):
        """/workspace/output yazılabilir olmalı (volume mount)."""
        code = "open('/workspace/output/test.txt', 'w').write('ok'); print('yazıldı')"
        res = kernel_exec(auth_headers, workspace_id, code)
        data = res.json()
        assert data["success"], f"/workspace/output yazılamadı: {data}"
        print(f"  ✅ Output volume yazılabilir: {data['output'].strip()}")


class TestForkBomb:
    """Saldırı 6: Fork Bomb (Adım 5) — PID limit koruması."""

    def test_fork_bomb_contained(self, auth_headers, workspace_id):
        """
        os.fork() × 100 → --pids-limit 50 ile kesilmeli.
        Sistem KİLİTLENMEMELİ.
        """
        code = """
import os
try:
    pids = []
    for i in range(100):
        pid = os.fork()
        if pid == 0:
            os._exit(0)
        pids.append(pid)
    print(f"TEHLIKE: {len(pids)} fork başarılı")
except OSError as e:
    print(f"ENGELLENDI: {e}")
except Exception as e:
    print(f"HATA: {e}")
"""
        res = kernel_exec(auth_headers, workspace_id, code, timeout=30)
        assert res.status_code == 200, f"API yanıt vermedi (timeout?): {res.status_code}"
        data = res.json()
        full_output = data.get("output", "") + data.get("error", "")

        assert "TEHLIKE: 100 fork başarılı" not in full_output, \
            "🔴 FORK BOMB SINIRLANMADI! 100 fork tamamlandı!"
        print(f"  ✅ Fork bomb engellendi/sınırlandı: {full_output[:120]}")

    def test_system_alive_after_fork_bomb(self, auth_headers, workspace_id):
        """Fork bomb sonrası sistem hâlâ çalışmalı."""
        # Kernel restart (fork bomb sonrası temizlik)
        requests.post(f"{BASE_URL}/kernel/{workspace_id}/restart", headers=auth_headers)
        time.sleep(3)

        res = kernel_exec(auth_headers, workspace_id, "print('hayattayım')")
        assert res.status_code == 200
        data = res.json()
        assert data["success"], f"Sistem fork bomb sonrası ÇÖKTÜ: {data}"
        assert "hayattayım" in data["output"]
        print(f"  ✅ Sistem fork bomb sonrası hayatta: '{data['output'].strip()}'")


class TestIPCIsolation:
    """Saldırı 7: IPC İzolasyonu (Adım 8) — Shared memory engellenmeli."""

    def test_shared_memory_blocked(self, auth_headers, workspace_id):
        """/dev/shm yazma veya shared_memory oluşturma → Hata."""
        code = """
try:
    f = open('/dev/shm/exploit', 'w')
    f.write('pwned')
    f.close()
    print('TEHLIKE: /dev/shm yazıldı!')
except (PermissionError, OSError) as e:
    print(f'ENGELLENDI: {e}')
except Exception as e:
    print(f'HATA: {e}')
"""
        res = kernel_exec(auth_headers, workspace_id, code)
        assert res.status_code == 200
        data = res.json()
        full_output = data.get("output", "") + data.get("error", "")

        assert "TEHLIKE" not in full_output, \
            "🔴 IPC İZOLASYONU KIRILDI! /dev/shm yazılabilir!"
        assert "ENGELLENDI" in full_output or not data["success"], \
            f"Beklenmeyen sonuç: {full_output}"
        print(f"  ✅ IPC izolasyonu çalışıyor: {full_output[:100]}")

    def test_multiprocessing_shared_memory_blocked(self, auth_headers, workspace_id):
        """multiprocessing.shared_memory → Hata."""
        code = """
try:
    from multiprocessing import shared_memory
    shm = shared_memory.SharedMemory(create=True, size=1024)
    print(f'TEHLIKE: SharedMemory oluşturuldu: {shm.name}')
    shm.close()
    shm.unlink()
except Exception as e:
    print(f'ENGELLENDI: {type(e).__name__}: {e}')
"""
        res = kernel_exec(auth_headers, workspace_id, code)
        assert res.status_code == 200
        data = res.json()
        full_output = data.get("output", "") + data.get("error", "")

        assert "TEHLIKE" not in full_output, \
            "🔴 SharedMemory OLUŞTURULDU!"
        print(f"  ✅ SharedMemory engellendi: {full_output[:100]}")


# ══════════════════════════════════════════════════════════════════
# BÖLÜM 3: BONUS — NETWORK ISOLATION & AUTH
# ══════════════════════════════════════════════════════════════════

class TestNetworkIsolation:
    """--network none koruması — Konteynerden dışarı çıkılamamalı."""

    def test_outbound_network_blocked(self, auth_headers, workspace_id):
        """Konteynerden HTTP isteği → Hata."""
        code = """
try:
    import urllib.request
    urllib.request.urlopen('http://google.com', timeout=3)
    print('TEHLIKE: Dış ağa erişildi!')
except Exception as e:
    print(f'ENGELLENDI: {type(e).__name__}')
"""
        res = kernel_exec(auth_headers, workspace_id, code, timeout=30)
        data = res.json()
        full_output = data.get("output", "") + data.get("error", "")
        assert "TEHLIKE" not in full_output, "🔴 KONTEYNER DIŞ AĞA ERİŞTİ!"
        print(f"  ✅ Network izolasyonu: {full_output[:80]}")


class TestFileControllerAuth:
    """FileController [Authorize] doğrulaması."""

    def test_file_api_requires_auth(self, workspace_id):
        """Token olmadan dosya listesi → 401."""
        res = requests.get(f"{BASE_URL}/workspace/{workspace_id}/file")
        assert res.status_code == 401, \
            f"🔴 FileController auth BYPASS! Status: {res.status_code}"
        print(f"  ✅ FileController auth zorunlu [401]")


# ══════════════════════════════════════════════════════════════════
# ANA GİRİŞ NOKTASI
# ══════════════════════════════════════════════════════════════════
if __name__ == "__main__":
    print("\n" + "═" * 60)
    print("  🏰 CURRERE FORTRESS — Red Team Sızma Testi")
    print("  8 Saldırı Vektörü × 7 Güvenlik Katmanı")
    print("═" * 60 + "\n")
    pytest.main([__file__, "-v", "--tb=short", "-s"])
