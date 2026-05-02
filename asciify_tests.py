import sys

mapping = {
    'ç': 'c', 'Ç': 'C',
    'ğ': 'g', 'Ğ': 'G',
    'ı': 'i', 'İ': 'I',
    'ö': 'o', 'Ö': 'O',
    'ş': 's', 'Ş': 'S',
    'ü': 'u', 'Ü': 'U',
    '↳': '->',
    '✅': '[OK]',
    '🛡️': '[GUARD]',
    '📦': '[BOX]',
    '🧪': '[TEST]',
    '💥': '[BOOM]',
    '🕵️': '[AGENT]',
    '\u21b3': '->',
    '\u2705': '[OK]'
}

with open('test_comprehensive.py', 'r', encoding='utf-8') as f:
    content = f.read()

for k, v in mapping.items():
    content = content.replace(k, v)

# Also remove any remaining non-ascii chars just in case
content = "".join([c if ord(c) < 128 else '?' for c in content])

with open('test_comprehensive.py', 'w', encoding='utf-8') as f:
    f.write(content)

print("Asciified test_comprehensive.py")
