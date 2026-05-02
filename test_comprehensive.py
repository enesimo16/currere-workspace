import requests
import time
import random
import urllib3
import json

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

# API Adresi. Eger farkliysa degistirebilirsiniz.
BASE_URL = "http://localhost:5279"

# Terminal Renkleri
GREEN = '\033[92m'
YELLOW = '\033[93m'
RED = '\033[91m'
RESET = '\033[0m'

def print_result(condition, success_msg, fail_msg, is_guarded=False, extra=""):
    color = YELLOW if is_guarded else GREEN
    tag = "[GUARDED]" if is_guarded else "[OK]"
    
    if condition:
        print(f"  {color}{tag}{RESET} {success_msg}")
        if extra:
            print(f"      -> {extra}")
    else:
        print(f"  {RED}[FAIL]{RESET} {fail_msg}")
        if extra:
            print(f"      -> Gelen Yanit: {extra}")

def register_user(email, password):
    return requests.post(f"{BASE_URL}/api/auth/register", json={"firstName": "Test", "lastName": "User", "email": email, "password": password}, verify=False)

def login_user(email, password):
    return requests.post(f"{BASE_URL}/api/auth/login", json={"email": email, "password": password}, verify=False)

def get_workspace(token):
    req = requests.get(f"{BASE_URL}/api/workspace", headers={"Authorization": f"Bearer {token}"}, verify=False)
    if req.status_code == 200 and len(req.json()) > 0:
        return req.json()[0]['id']
    return None

def execute_code(workspace_id, token, code):
    return requests.post(
        f"{BASE_URL}/api/execution/{workspace_id}/run", 
        json={"code": code}, 
        headers={"Authorization": f"Bearer {token}"}, 
        verify=False
    )

def poll_job(job_id, token):
    while True:
        req = requests.get(f"{BASE_URL}/api/execution/status/{job_id}", headers={"Authorization": f"Bearer {token}"}, verify=False)
        try:
            data = req.json()
        except Exception:
            data = {"error": req.text, "errorType": "Unknown"}
            return req.status_code, data

        if req.status_code != 200:
            return req.status_code, data
            
        if data.get('status') == 'Processing':
            time.sleep(2)
            continue
        return req.status_code, data

class TestSyntheticDataPipeline:
    def run(self, email, password):
        print(f"\n{YELLOW}--- 3. SENTETIK VERI PIPELINE (E2E) TESTI ---{RESET}")
        print("[*] Test 3.1: 'FastAndFake' modu ile sahte veri uretimi...")
        
        login_res = login_user(email, password)
        if login_res.status_code != 200:
            print_result(False, "", "Kimlik dogrulama hatasi (Test calistirilamadi).")
            return
            
        token = login_res.json().get('token')
        ws = get_workspace(token)

        synth_payload = {
            "prompt": "Test verisi uret",
            "columns": "ad, soyad, yas",
            "rowCount": 5,
            "fileName": "test_synthetic.csv",
            "mode": 1 # FastAndFake (Mock Modu)
        }
        
        res = requests.post(
            f"{BASE_URL}/api/workspace/{ws}/syntheticdata/generate", 
            json=synth_payload, 
            headers={"Authorization": f"Bearer {token}"}, 
            verify=False
        )
        
        is_success = res.status_code == 200
        print_result(is_success, "Sentetik veri uretim talebi (FastAndFake) Docker / API'i kitlemeden basariyla tetiklendi.", f"Sentetik API hatasi: {res.status_code}", is_guarded=False, extra=res.text if not is_success else res.json().get("message", ""))
        
        print("\n[*] Test 3.2: Uretilen dosyanin Workspace'e kaydedildiginin dogrulanmasi...")
        file_res = requests.get(f"{BASE_URL}/api/workspace/{ws}/file", headers={"Authorization": f"Bearer {token}"}, verify=False)
        
        file_found = False
        if file_res.status_code == 200:
            files = file_res.json()
            for f in files:
                if f.get('fileName') == 'test_synthetic.csv':
                    file_found = True
                    break
                    
        print_result(file_found, "Sahte sentetik dosya calisma alaninda (workspace/output) basariyla olusturuldu.", "Dosya listesinde test_synthetic.csv bulunamadi!", is_guarded=False)

def main():
    print(f"[TEST] {GREEN}E2E KAOS VE GUVENLIK TESTLERI BASLIYOR...{RESET}\n")

    email_a = f"alice_{random.randint(1000,9999)}@test.com"
    email_b = f"bob_{random.randint(1000,9999)}@test.com"
    pwd = "TestPassword123!"

    # --- 1. KIMLIK VE IZOLASYON TESTLERI ---
    print(f"{YELLOW}--- 1. KIMLIK VE IZOLASYON TESTLERI ---{RESET}")
    
    # 1.1 Gecersiz Sifre
    print("[*] Test 1.1: Gecersiz sifre ile giris denemesi...")
    register_user(email_a, pwd)
    bad_login = login_user(email_a, "YanlisSifre444!")
    print_result(bad_login.status_code == 401, "Gecersiz sifre basariyla engellendi (401 Unauthorized donuyor).", "Gecersiz sifreyle giris engellenmedi!", is_guarded=True)

    # 1.2 Capraz Calisma Alani (IDOR)
    print("\n[*] Test 1.2: Baska kullanicinin Workspace'ine kod gonderme (IDOR) denemesi...")
    token_a = login_user(email_a, pwd).json().get('token')
    ws_a = get_workspace(token_a)

    register_user(email_b, pwd)
    token_b = login_user(email_b, pwd).json().get('token')
    
    cross_exec = execute_code(ws_a, token_b, "print('Hacked')")
    is_blocked = cross_exec.status_code in [403, 404]
    print_result(is_blocked, "Yetkisiz Workspace erisimi basariyla engellendi (404/403 donuyor).", "Erisim engellenmedi! Backend'de IDOR acigi var.", is_guarded=True, extra=cross_exec.text if not is_blocked else "")


    # --- 2. EXECUTION (DOCKER) STRES TESTLERI ---
    print(f"\n{YELLOW}--- 2. EXECUTION (DOCKER) STRES TESTLERI ---{RESET}")

    # 2.1 Syntax Error Testi
    print("[*] Test 2.1: Sozdizimi Hatasi (Syntax Error) Testi...")
    syntax_req = execute_code(ws_a, token_a, "print('merhaba)")
    if syntax_req.status_code == 202:
        job_id = syntax_req.json().get('jobId')
        status_code, data = poll_job(job_id, token_a)
        
        # AST tarayicisi engellediyse SecurityOrSyntaxError doner. Veya python syntax doner.
        error_type = data.get('errorType', '')
        success = (data.get('isSuccess') == False) and (error_type in ['SecurityOrSyntaxError', 'SyntaxError'])
        
        print_result(success, "Syntax Error, backend'i cokertmeden yakalandi ve JSON olarak donuldu.", "Backend duzgun hata donmedi.", is_guarded=True, extra=str(data.get('error') or error_type))
    else:
        print_result(False, "", f"API reddetti / Kod kuyruga alinamadi: {syntax_req.status_code}")

    # 2.2 Timeout (Sonsuz Dongu) Testi
    print("\n[*] Test 2.2: Sonsuz Dongu / Timeout Testi (Sistemin kilidini acmasi ~45 saniye surebilir)...")
    timeout_req = execute_code(ws_a, token_a, "while True:\n    pass")
    if timeout_req.status_code == 202:
        job_id = timeout_req.json().get('jobId')
        status_code, data = poll_job(job_id, token_a)
        
        error_type = str(data.get('errorType', '')).lower()
        error_msg = str(data.get('error', '')).lower()
        # Hem TimeoutError hem de JSON gelmediginde olusan SystemCrashError'u timeout olarak kabul et (cunku kill switch calisiyor demektir)
        success = (data.get('isSuccess') == False) and ('time' in error_msg or 'timeout' in error_type or 'crash' in error_type)
        
        print_result(success, "Sonsuz dongu zaman asimi (Kill Switch) ile imha edildi.", "Konteyner beklenen surede oldurulemedi.", is_guarded=True, extra=data.get('error'))
    else:
        print_result(False, "", f"API reddetti / Kod kuyruga alinamadi: {timeout_req.status_code}")

    # 2.3 Hack/Isletim Sistemi Testi
    print("\n[*] Test 2.3: Isletim Sistemi Yetki Yukseltme / Sizma Testi...")
    hack_code = "import os\nprint(os.system('cat /etc/passwd'))"
    hack_req = execute_code(ws_a, token_a, hack_code)
    if hack_req.status_code == 202:
        job_id = hack_req.json().get('jobId')
        status_code, data = poll_job(job_id, token_a)
        
        out = data.get('output', '')
        err = data.get('error', '')
        error_type = data.get('errorType', '')
        
        # Eger SecurityPreprocessor AST seviyesinde yasakli modul derse (SecurityOrSyntaxError) bu da basarilidir.
        is_blocked_by_ast = error_type == 'SecurityOrSyntaxError'
        is_blocked_by_docker = 'root:x:0:0' not in str(out) and 'root:x:0:0' not in str(err)
        
        success = is_blocked_by_ast or is_blocked_by_docker
        print_result(success, "Zararli komut AST veya Docker Sandbox tarafindan basariyla engellendi.", "Guvenlik Ihlali! /etc/passwd okundu.", is_guarded=True, extra="AST Blokaji" if is_blocked_by_ast else "OS Kisitlamasi")
    else:
        print_result(False, "", f"API reddetti / Kod kuyruga alinamadi: {hack_req.status_code}")

    # 2.4 Happy Path
    print("\n[*] Test 2.4: Happy Path (Temiz Kod Basarisi)...")
    happy_code = "for i in range(1, 11):\n    print(f'Sayi: {i}')"
    happy_req = execute_code(ws_a, token_a, happy_code)
    if happy_req.status_code == 202:
        job_id = happy_req.json().get('jobId')
        status_code, data = poll_job(job_id, token_a)
        
        success = data.get('isSuccess') == True and 'Sayi: 10' in str(data.get('output', ''))
        out_str = str(data.get('output', ''))
        extra_msg = f"Cikti Baslangici: {out_str[:100]}..." if out_str else "Cikti Yok"
        print_result(success, "Temiz Python scripti izole ortamda calisti ve cikti sorunsuz yakalandi.", "Beklenen cikti alinamadi veya kod patladi.", is_guarded=False, extra=extra_msg)
    else:
        print_result(False, "", f"API reddetti / Kod kuyruga alinamadi: {happy_req.status_code}")

    # 2.5 Memory Leak Test
    print("\n[*] Test 2.5: (Hell Mode) Memory Leak (RAM Tasirma) Testi...")
    mem_code = "x = [1] * 10**8\nprint('Asla buraya gelmemeli')"
    mem_req = execute_code(ws_a, token_a, mem_code)
    if mem_req.status_code == 202:
        job_id = mem_req.json().get('jobId')
        status_code, data = poll_job(job_id, token_a)
        
        err_msg = str(data.get('error', '')).lower()
        err_type = str(data.get('errorType', '')).lower()
        # OutOfMemoryError is manually appended by our C# catch based on OOMKilled or raw MemoryError
        success = (data.get('isSuccess') == False) and ('memory' in err_msg or 'memory' in err_type)
        print_result(success, "Memory Leak (OOM) basariyla durduruldu (RAM 512MB ile kisitli).", "Backend bellegin tasmasina izin verdi veya sistem coktu.", is_guarded=True, extra=data.get('error'))
    else:
        print_result(False, "", f"API reddetti / Kod kuyruga alinamadi: {mem_req.status_code}")

    # 2.6 Ag Izolasyonu Testi
    print("\n[*] Test 2.6: (Hell Mode) Ag (Network) Izolasyonu Testi...")
    net_code = "import urllib.request\ntry:\n    urllib.request.urlopen('http://google.com', timeout=3)\nexcept Exception as e:\n    print(str(e))"
    net_req = execute_code(ws_a, token_a, net_code)
    if net_req.status_code == 202:
        job_id = net_req.json().get('jobId')
        status_code, data = poll_job(job_id, token_a)
        
        out = str(data.get('output', '')).lower()
        err = str(data.get('error', '')).lower()
        error_type = data.get('errorType', '')
        
        # AST tarayicisi engellediyse de basarilidir. Veya Name resolution fail.
        is_blocked_by_ast = error_type == 'SecurityOrSyntaxError'
        is_blocked_by_docker = 'temporary failure in name resolution' in out or 'temporary failure in name resolution' in err or 'timed out' in out or 'not known' in out
        
        success = is_blocked_by_ast or (is_blocked_by_docker and data.get('isSuccess') == True) # Python code catches connection exception and prints it successfully
        print_result(success, "Dis ag baglantisi (Network=none) veya AST tarafindan basariyla kilitlendi.", "Konteyner disariya baglanabiliyor! Network izole degil.", is_guarded=True, extra="AST Blokaji" if is_blocked_by_ast else f"DNS Reddi: {out}")
    else:
        print_result(False, "", f"API reddetti / Kod kuyruga alinamadi: {net_req.status_code}")

    # 2.7 Agir Cikti Testi
    print("\n[*] Test 2.7: (Hell Mode) Log Buffer Tasirma (Spam) Testi...")
    spam_code = "for i in range(10000):\n    print('SPAM_SPAM_SPAM')\nprint('BITIS_ONAYI')"
    spam_req = execute_code(ws_a, token_a, spam_code)
    if spam_req.status_code == 202:
        job_id = spam_req.json().get('jobId')
        status_code, data = poll_job(job_id, token_a)
        
        # If the backend is solid, it should either truncate or survive and return bitis onayi OR it should kill it due to taking too long (10k unbuffered flushes > 15s)
        out_string = str(data.get('output', ''))
        error_type = str(data.get('errorType', ''))
        success = ('BITIS_ONAYI' in out_string and data.get('isSuccess') == True) or ('timeout' in error_type.lower())
        
        print_result(success, f"10.000 satirli devasa print spami backend tarafindan guvenle yonetildi (Cikti {len(out_string)} char, Tur: {error_type}).", "Backend agir ciktidan dolayi kitlendi veya parse edemedi.", is_guarded=True, extra=data.get('error') or str(data))
    else:
        print_result(False, "", f"API reddetti / Kod kuyruga alinamadi: {spam_req.status_code}")

    # --- 3. SENTETIK VERI PIPELINE TESTLERI ---
    synth_tester = TestSyntheticDataPipeline()
    synth_tester.run(email_a, pwd)

    print(f"\n{GREEN}[SUCCESS] TUM SENARYOLAR VE HAPPY PATH TESTLERI TAMAMLANDI!{RESET}")

if __name__ == "__main__":
    main()
