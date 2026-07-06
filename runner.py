"""
Currere Runner v3.0 — Plot-Aware Edition
=========================================
Her adımda [DEBUG] logu basarak konteynerin hangi satırda takıldığını gösterir.
v3.0 değişiklikler:
  - Matplotlib monkey-patch: plt.show() / fig.show() artık /workspace/output/*.png kaydeder
  - Output dizini garantili oluşturulur
  - Grafik dosyaları JSON yanıtında 'plots' listesiyle raporlanır
"""
import sys
import json
import io
import base64
import os
import re
import signal

# ── ZORUNLU TIMEOUT SİGORTASI ────────────────────────────────────────────────
def _timeout_handler(signum, frame):
    print(json.dumps({
        'success': False,
        'error_type': 'TimeoutError',
        'message': 'Runner dahili timeout: Kod 40 saniye içinde tamamlanamadı.',
        'plots': []
    }), flush=True)
    sys.stdout.flush()
    import time
    time.sleep(0.5)
    sys.exit(1)

if hasattr(signal, 'SIGALRM'):
    signal.signal(signal.SIGALRM, _timeout_handler)
    signal.alarm(40)

print("[DEBUG] Runner v3.0 başlatıldı", flush=True)

# ── ADIM 1: ÇALIŞMA DİZİNİNİ SABİTLE ────────────────────────────────────────
print("[DEBUG] os.chdir('/workspace') çağrılıyor...", flush=True)
try:
    os.chdir('/workspace')
    print(f"[DEBUG] Çalışma dizini: {os.getcwd()}", flush=True)
except Exception as e:
    print(f"[ERROR] os.chdir başarısız: {e}", flush=True)

# ── ADIM 2: OUTPUT DİZİNİNİ HAZIRLA (KRİTİK) ────────────────────────────────
OUTPUT_DIR = '/workspace/output'
try:
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    print(f"[DEBUG] Output dizini hazır: {OUTPUT_DIR}", flush=True)
except Exception as e:
    print(f"[ERROR] Output dizini oluşturulamadı: {e}", flush=True)
    # /tmp fallback
    OUTPUT_DIR = '/tmp/output'
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    print(f"[DEBUG] Fallback output dizini: {OUTPUT_DIR}", flush=True)

# ── ADIM 3: SYMLINK KÖPRÜSÜ ─────────────────────────────────────────────────
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

        if not os.path.isfile(full_path):
            print(f"[DEBUG] Atlanıyor (dosya değil): {filename}", flush=True)
            continue

        match = GUID_PREFIX_PATTERN.match(filename)
        original_name = match.group(1) if match else filename

        try:
            mtime = os.path.getmtime(full_path)
        except OSError:
            mtime = 0

        if original_name not in best_candidates or mtime > best_candidates[original_name][1]:
            best_candidates[original_name] = (full_path, mtime)
            print(f"[DEBUG] Aday dosya: {filename} → {original_name}", flush=True)

    for original_name, (source_path, _) in best_candidates.items():
        link_path = os.path.join('/workspace', original_name)
        try:
            if os.path.islink(link_path) or os.path.exists(link_path):
                os.remove(link_path)
            os.symlink(source_path, link_path)
            print(f"[DEBUG] Symlink oluşturuldu: {original_name} → {source_path}", flush=True)
        except OSError as e:
            print(f"[ERROR] Symlink başarısız ({original_name}): {e}", flush=True)

    print(f"[DEBUG] Symlink adımı tamamlandı. Toplam: {len(best_candidates)} dosya", flush=True)
else:
    print("[DEBUG] Data klasörü yok, symlink adımı atlanıyor", flush=True)

# ── ADIM 4: PYTHONPATH ───────────────────────────────────────────────────────
site_packages_dir = '/workspace/site-packages'
if os.path.isdir(site_packages_dir) and site_packages_dir not in sys.path:
    sys.path.insert(0, site_packages_dir)
    print(f"[DEBUG] site-packages eklendi: {site_packages_dir}", flush=True)

# ── ADIM 5: MATPLOTLİB MONKEY-PATCH ─────────────────────────────────────────
# plt.show() ve fig.show() çağrılarını yakalayıp dosyaya kaydet.
# Bu patch, exec() ÖNCE yapılmalı ki kullanıcı kodu bunu görsün.
_plot_counter = [0]  # mutable liste, closure'da değiştirilebilir
_saved_plots = []    # kaydedilen dosya yolları

def _setup_matplotlib_patch():
    """
    Matplotlib yüklüyse:
      - Backend'i 'Agg' (headless) olarak zorla
      - plt.show() ve Figure.show() metodlarını override et
      - plt.savefig() çağrılarını output dizinine yönlendir
    """
    try:
        import matplotlib
        matplotlib.use('Agg')
        import matplotlib.pyplot as plt

        # Orijinal fonksiyonları sakla
        _orig_show = plt.show
        _orig_savefig = plt.savefig

        def _patched_show(*args, **kwargs):
            """plt.show() çağrısını yakalar, grafiği output'a kaydeder."""
            try:
                _plot_counter[0] += 1
                out_path = os.path.join(OUTPUT_DIR, f'plot_{_plot_counter[0]:03d}.png')
                plt.savefig(out_path, bbox_inches='tight', dpi=150)
                plt.close('all')
                _saved_plots.append(out_path)
                print(f"[DEBUG] plt.show() yakalandı → {out_path}", file=sys.__stdout__, flush=True)
            except Exception as e:
                print(f"[ERROR] _patched_show hata: {e}", file=sys.__stdout__, flush=True)

        def _patched_savefig(fname, *args, **kwargs):
            """plt.savefig() çağrılarını output dizinine yönlendir."""
            try:
                # Eğer kullanıcı mutlak bir yol vermişse output'a kopyala
                if not os.path.isabs(str(fname)):
                    fname = os.path.join(OUTPUT_DIR, os.path.basename(str(fname)))
                _orig_savefig(fname, *args, **kwargs)
                if str(fname) not in _saved_plots:
                    _saved_plots.append(str(fname))
                print(f"[DEBUG] plt.savefig() yakalandı → {fname}", file=sys.__stdout__, flush=True)
            except Exception as e:
                print(f"[ERROR] _patched_savefig hata: {e}", file=sys.__stdout__, flush=True)
                # Orijinal fonksiyonu dene
                try:
                    _orig_savefig(fname, *args, **kwargs)
                except Exception:
                    pass

        plt.show = _patched_show
        plt.savefig = _patched_savefig

        # Figure.show() için de patch ekle
        try:
            from matplotlib.figure import Figure
            _orig_fig_show = Figure.show
            def _patched_fig_show(self, *args, **kwargs):
                try:
                    _plot_counter[0] += 1
                    out_path = os.path.join(OUTPUT_DIR, f'plot_{_plot_counter[0]:03d}.png')
                    self.savefig(out_path, bbox_inches='tight', dpi=150)
                    _saved_plots.append(out_path)
                    print(f"[DEBUG] fig.show() yakalandı → {out_path}", file=sys.__stdout__, flush=True)
                except Exception as e:
                    print(f"[ERROR] _patched_fig_show hata: {e}", file=sys.__stdout__, flush=True)
            Figure.show = _patched_fig_show
        except Exception as e:
            print(f"[DEBUG] Figure.show patch atlandı: {e}", file=sys.__stdout__, flush=True)

        print("[DEBUG] Matplotlib monkey-patch başarıyla uygulandı", flush=True)
        return True

    except ImportError:
        print("[DEBUG] Matplotlib bulunamadı, patch atlandı", flush=True)
        return False

_matplotlib_patched = _setup_matplotlib_patch()

# ── ADIM 6: KULLANICI KODUNU ÇALIŞTIR ────────────────────────────────────────
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

    sys.stdout = redirected_output
    sys.stderr = redirected_error

    sandbox_globals = {
        '__name__': '__main__',
        '__builtins__': __builtins__,
    }

    exec(user_code, sandbox_globals)

    sys.stdout = old_stdout
    sys.stderr = old_stderr
    print("[DEBUG] exec() başarıyla tamamlandı", flush=True)

    user_output = redirected_output.getvalue()
    user_errors = redirected_error.getvalue()

    combined_output = user_output.strip()
    if user_errors.strip():
        combined_output = (combined_output + "\n" + user_errors.strip()) if combined_output else user_errors.strip()

    # Output dizinini tara — exec() sırasında kaydedilen ek dosyaları da yakala
    try:
        if os.path.isdir(OUTPUT_DIR):
            for fname in os.listdir(OUTPUT_DIR):
                fpath = os.path.join(OUTPUT_DIR, fname)
                if fpath not in _saved_plots and os.path.isfile(fpath):
                    ext = os.path.splitext(fname)[1].lower()
                    if ext in ('.png', '.jpg', '.jpeg', '.svg'):
                        _saved_plots.append(fpath)
                        print(f"[DEBUG] Ek grafik tespit edildi: {fpath}", flush=True)
        print(f"[DEBUG] Toplam kaydedilen grafik: {len(_saved_plots)}", flush=True)
    except Exception as e:
        print(f"[ERROR] Output dizini tarama hatası: {e}", flush=True)

    result = json.dumps({
        'success': True,
        'error_type': None,
        'message': combined_output,
        'plots': _saved_plots
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
        'message': error_msg,
        'plots': _saved_plots
    })
    print(result, flush=True)

# ── ZORUNLU ÇIKIŞ ────────────────────────────────────────────────────────────
print("[DEBUG] Runner tamamlandı, sys.exit(0) çağrılıyor", flush=True)
sys.exit(0)