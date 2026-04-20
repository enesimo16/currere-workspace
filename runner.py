import sys
import json
import io
import traceback
import base64
import os

old_stdout = sys.stdout
redirected_output = io.StringIO()
sys.stdout = redirected_output

try:
    # base64 kodunu ortam değişkeninden (Environment Variable) 
    base64_code = os.environ.get('CODE_TO_RUN', '')
    if not base64_code:
        raise ValueError("Çalıştırılacak kod bulunamadı (CODE_TO_RUN eksik).")

    # Kod çöz ve çalış
    user_code = base64.b64decode(base64_code).decode('utf-8')
    exec(user_code, globals())
    
    sys.stdout = old_stdout
    user_output = redirected_output.getvalue()
    
    # Başarılı JSON 
    print(json.dumps({
        'success': True,
        'error_type': None,
        'message': user_output.strip()
    }))
except Exception as e:
    sys.stdout = old_stdout
    error_type = type(e).__name__
    error_msg = str(e)
    
    # Hatalı JSON 
    print(json.dumps({
        'success': False,
        'error_type': error_type,
        'message': error_msg
    }))