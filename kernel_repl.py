"""
Currere Stateful Kernel REPL v1.0
==================================
Docker konteyneri içinde sürekli çalışan interaktif Python kernel.
C# tarafından stdin üzerinden JSON komutu alır, exec() ile çalıştırır,
sonucu JSON olarak stdout'a yazar.

Önemli: shared_globals sayesinde değişkenler hücreler arası yaşar.
"""
import sys
import json
import io
import base64
import os
import signal
import traceback

# ── ÇALIŞMA DİZİNİ ───────────────────────────────────────────────────────────
os.chdir('/workspace')

# ── PAYLAŞILAN GLOBAL SÖZLÜK (Stateful Hafıza) ──────────────────────────────
# Tüm exec() çağrıları bu sözlük içinde çalışır.
# Böylece hücre 1'de tanımlanan df değişkeni hücre 2'de kullanılabilir.
shared_globals = {
    '__name__': '__main__',
    '__builtins__': __builtins__,
}

# ── SYMLINK KÖPRÜSÜ (runner.py ile aynı mantık) ──────────────────────────────
import re
DATA_DIR = '/workspace/data'
GUID_PREFIX_PATTERN = re.compile(r'^[a-f0-9]{8}_(.+)$')

def create_workspace_symlinks():
    if not os.path.isdir(DATA_DIR):
        return
    best_candidates = {}
    for filename in os.listdir(DATA_DIR):
        full_path = os.path.join(DATA_DIR, filename)
        if not os.path.isfile(full_path):
            continue
        match = GUID_PREFIX_PATTERN.match(filename)
        original_name = match.group(1) if match else filename
        try:
            mtime = os.path.getmtime(full_path)
        except OSError:
            mtime = 0
        if original_name not in best_candidates or mtime > best_candidates[original_name][1]:
            best_candidates[original_name] = (full_path, mtime)
    for original_name, (source_path, _) in best_candidates.items():
        link_path = os.path.join('/workspace', original_name)
        try:
            if os.path.islink(link_path) or os.path.exists(link_path):
                os.remove(link_path)
            os.symlink(source_path, link_path)
        except OSError:
            pass

# İlk başlangıçta symlink'leri kur
create_workspace_symlinks()

# ── HAZIR SINYAL ──────────────────────────────────────────────────────────────
# C#'a kernel'ın hazır olduğunu bildir
ready_signal = json.dumps({"type": "ready", "message": "Kernel hazır"})
sys.stdout.write(ready_signal + "\n")
sys.stdout.flush()

# ── ANA REPL DÖNGÜSÜ ─────────────────────────────────────────────────────────
while True:
    try:
        # stdin'den bir satır oku (C# WriteLineAsync ile gönderir)
        line = sys.stdin.readline()
        
        # stdin kapandıysa (C# process'i öldürdüyse) çık
        if not line:
            break
        
        line = line.strip()
        if not line:
            continue
        
        # JSON komutunu parse et
        try:
            command = json.loads(line)
        except json.JSONDecodeError:
            error_response = json.dumps({
                "type": "error",
                "success": False,
                "error_type": "ProtocolError",
                "message": "Geçersiz JSON komutu"
            })
            sys.stdout.write(error_response + "\n")
            sys.stdout.flush()
            continue
        
        action = command.get("action", "execute")
        
        # ── EXECUTE KOMUTU ────────────────────────────────────────────────
        if action == "execute":
            code_b64 = command.get("code", "")
            if not code_b64:
                result = json.dumps({
                    "type": "result",
                    "success": False,
                    "error_type": "ValueError",
                    "message": "Boş kod gönderildi"
                })
                sys.stdout.write(result + "\n")
                sys.stdout.flush()
                continue
            
            # Base64'ten kodu çöz
            try:
                user_code = base64.b64decode(code_b64).decode('utf-8')
            except Exception as e:
                result = json.dumps({
                    "type": "result",
                    "success": False,
                    "error_type": "DecodeError",
                    "message": f"Kod çözülemedi: {str(e)}"
                })
                sys.stdout.write(result + "\n")
                sys.stdout.flush()
                continue
            
            # stdout/stderr yakala
            captured_stdout = io.StringIO()
            captured_stderr = io.StringIO()
            old_stdout = sys.stdout
            old_stderr = sys.stderr
            sys.stdout = captured_stdout
            sys.stderr = captured_stderr
            
            success = True
            error_type = None
            error_msg = None
            
            try:
                exec(user_code, shared_globals)
            except Exception as e:
                success = False
                error_type = type(e).__name__
                error_msg = str(e)
                # Traceback'i stderr'e yaz
                traceback.print_exc(file=captured_stderr)
            
            # stdout/stderr'i geri yükle
            sys.stdout = old_stdout
            sys.stderr = old_stderr
            
            stdout_text = captured_stdout.getvalue()
            stderr_text = captured_stderr.getvalue()
            
            output = stdout_text.strip()
            if stderr_text.strip() and success:
                # Warning gibi stderr çıktıları varsa ama exception yoksa ekle
                output = (output + "\n" + stderr_text.strip()).strip()
            
            if success:
                result = json.dumps({
                    "type": "result",
                    "success": True,
                    "error_type": None,
                    "message": output
                })
            else:
                result = json.dumps({
                    "type": "result",
                    "success": False,
                    "error_type": error_type,
                    "message": error_msg or stderr_text.strip()
                })
            
            sys.stdout.write(result + "\n")
            sys.stdout.flush()
        
        # ── PING KOMUTU (Sağlık kontrolü) ────────────────────────────────
        elif action == "ping":
            pong = json.dumps({"type": "pong", "success": True, "message": "alive"})
            sys.stdout.write(pong + "\n")
            sys.stdout.flush()
        
        # ── RESET KOMUTU (Hafızayı temizle) ──────────────────────────────
        elif action == "reset":
            shared_globals.clear()
            shared_globals['__name__'] = '__main__'
            shared_globals['__builtins__'] = __builtins__
            create_workspace_symlinks()  # Symlink'leri yenile
            reset_result = json.dumps({
                "type": "result",
                "success": True,
                "message": "Kernel hafızası temizlendi."
            })
            sys.stdout.write(reset_result + "\n")
            sys.stdout.flush()
        
        else:
            unknown = json.dumps({
                "type": "error",
                "success": False,
                "error_type": "UnknownAction",
                "message": f"Bilinmeyen aksiyon: {action}"
            })
            sys.stdout.write(unknown + "\n")
            sys.stdout.flush()
    
    except KeyboardInterrupt:
        break
    except Exception as e:
        # Döngü asla kırılmasın — hata olursa logla ve devam et
        try:
            crash = json.dumps({
                "type": "error",
                "success": False,
                "error_type": "KernelCrash",
                "message": f"Kernel iç hatası: {str(e)}"
            })
            sys.stdout.write(crash + "\n")
            sys.stdout.flush()
        except:
            pass
