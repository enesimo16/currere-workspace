"""
Currere Runner v2.1 — DEBUG VERSION
====================================
Her adımda [DEBUG] logu basarak konteynerin hangi satırda takıldığını gösterir.
"""
import sys
import json
import io
import base64
import os
import re
import signal

# ── ZORUNLU TIMEOUT SİGORTASI ────────────────────────────────────────────────
# Eğer exec() sonsuz döngüye girerse veya asılı kalırsa, bu sinyal 40 saniye
# sonra prosesi zorla öldürür (C# tarafındaki 45sn timeout'tan önce).
def _timeout_handler(signum, frame):
    print(json.dumps({
        'success': False,
        'error_type': 'TimeoutError',
        'message': 'Runner dahili timeout: Kod 40 saniye içinde tamamlanamadı.'
    }), flush=True)
    sys.stdout.flush()
    import time
    time.sleep(0.5)
    sys.exit(1)

# SIGALRM sadece Unix/Linux'ta çalışır (Docker Linux konteyneri)
if hasattr(signal, 'SIGALRM'):
    signal.signal(signal.SIGALRM, _timeout_handler)
    signal.alarm(40)

print("[DEBUG] Runner v2.1 başlatıldı", flush=True)

# ── ADIM 1: ÇALIŞMA DİZİNİNİ SABİTLE ────────────────────────────────────────
print("[DEBUG] os.chdir('/workspace') çağrılıyor...", flush=True)
try:
    os.chdir('/workspace')
    print(f"[DEBUG] Çalışma dizini: {os.getcwd()}", flush=True)
except Exception as e:
    print(f"[ERROR] os.chdir başarısız: {e}", flush=True)

# ── ADIM 2: SYMLINK KÖPRÜSÜ ─────────────────────────────────────────────────
DATA_DIR = '/workspace/data'
GUID_PREFIX_PATTERN = re.compile(r'^[a-f0-9]{8}_(.+)$')

print(f"[DEBUG] Data klasörü kontrol ediliyor: {DATA_DIR}", flush=True)
print(f"[DEBUG] Data klasörü var mı: {os.path.isdir(DATA_DIR)}", flush=True)

if os.path.isdir(DATA_DIR):
    try:
        entries = os.listdir(DATA_DIR)
        print(f"[DEBUG] Data klasöründe {len(entries)} öğe bulundu", flush=True)
    except Exception as e:
        print(f"[ERROR] os.listdir başarısız: {e}", flush=True)
        entries = []

    best_candidates = {}

    for filename in entries:
        full_path = os.path.join(DATA_DIR, filename)

        # SADECE düz dosyalar — klasörleri ve alt dizinleri ATLA
        if not os.path.isfile(full_path):
            print(f"[DEBUG] Atlanıyor (dosya değil): {filename}", flush=True)
            continue

        match = GUID_PREFIX_PATTERN.match(filename)
        if match:
            original_name = match.group(1)
        else:
            original_name = filename

        try:
            mtime = os.path.getmtime(full_path)
        except OSError:
            mtime = 0

        if original_name not in best_candidates or mtime > best_candidates[original_name][1]:
            best_candidates[original_name] = (full_path, mtime)
            print(f"[DEBUG] Aday dosya: {filename} → {original_name}", flush=True)

    # Symlink'leri oluştur
    for original_name, (source_path, _) in best_candidates.items():
        link_path = os.path.join('/workspace', original_name)
        try:
            if os.path.islink(link_path) or os.path.exists(link_path):
                os.remove(link_path)
            os.symlink(source_path, link_path)
            print(f"[DEBUG] Symlink oluşturuldu: {original_name} → {source_path}", flush=True)
        except OSError as e:
            print(f"[ERROR] Symlink başarısız ({original_name}): {e}", flush=True)
            # Kodu kırma, sonraki dosyaya geç

    print(f"[DEBUG] Symlink adımı tamamlandı. Toplam: {len(best_candidates)} dosya", flush=True)
else:
    print("[DEBUG] Data klasörü yok, symlink adımı atlanıyor", flush=True)

# ── ADIM 3: PYTHONPATH ───────────────────────────────────────────────────────
site_packages_dir = '/workspace/site-packages'
if os.path.isdir(site_packages_dir) and site_packages_dir not in sys.path:
    sys.path.insert(0, site_packages_dir)
    print(f"[DEBUG] site-packages eklendi: {site_packages_dir}", flush=True)

# ── ADIM 4: KULLANICI KODUNU ÇALIŞTIR ────────────────────────────────────────
print("[DEBUG] Kullanıcı kodu çözümleniyor...", flush=True)

old_stdout = sys.stdout
old_stderr = sys.stderr
redirected_output = io.StringIO()
redirected_error = io.StringIO()

try:
    base64_code = os.environ.get('CODE_TO_RUN', '')
    if not base64_code:
        raise ValueError("Çalıştırılacak kod bulunamadı (CODE_TO_RUN eksik).")

    user_code = base64.b64decode(base64_code).decode('utf-8')
    print(f"[DEBUG] Kod çözüldü ({len(user_code)} karakter). exec() başlatılıyor...", flush=True)

    # Stdout/stderr yönlendirmesi — DEBUG logları BURADAN SONRA kullanıcı çıktısına karışmaz
    sys.stdout = redirected_output
    sys.stderr = redirected_error

    sandbox_globals = {
        '__name__': '__main__',
        '__builtins__': __builtins__,
    }

    exec(user_code, sandbox_globals)

    # Geri yükle
    sys.stdout = old_stdout
    sys.stderr = old_stderr
    print("[DEBUG] exec() başarıyla tamamlandı", flush=True)

    user_output = redirected_output.getvalue()
    user_errors = redirected_error.getvalue()

    combined_output = user_output.strip()
    if user_errors.strip():
        combined_output = combined_output + "\n" + user_errors.strip() if combined_output else user_errors.strip()

    # Başarılı JSON
    result = json.dumps({
        'success': True,
        'error_type': None,
        'message': combined_output
    })
    print(result, flush=True)

except Exception as e:
    sys.stdout = old_stdout
    sys.stderr = old_stderr
    error_type = type(e).__name__
    error_msg = str(e)
    print(f"[DEBUG] exec() HATA: {error_type}: {error_msg}", flush=True)

    result = json.dumps({
        'success': False,
        'error_type': error_type,
        'message': error_msg
    })
    print(result, flush=True)

# ── ZORUNLU ÇIKIŞ ────────────────────────────────────────────────────────────
print("[DEBUG] Runner tamamlandı, sys.exit(0) çağrılıyor", flush=True)
sys.exit(0)