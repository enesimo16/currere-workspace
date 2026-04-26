import sys
import json
import ast

def parse_ipynb_to_py(content: str) -> str:
    """
    Attempts to parse the input as a Jupyter Notebook (JSON).
    If successful, converts markdown cells to comments and extracts code cells.
    Returns the pure Python script.
    If it's not valid JSON, returns the original content assuming it's already Python.
    """
    try:
        notebook = json.loads(content)
        if "cells" not in notebook:
            return content  # Not a standard ipynb format, treat as plain python
        
        py_lines = []
        for cell in notebook.get("cells", []):
            cell_type = cell.get("cell_type", "")
            source = cell.get("source", [])
            
            if isinstance(source, str):
                source = [source]
                
            if cell_type == "markdown":
                for line in source:
                    # Comment out markdown lines
                    py_lines.append(f"# {line.rstrip()}")
                py_lines.append("") # Empty line after cell
            elif cell_type == "code":
                for line in source:
                    py_lines.append(line.rstrip("\n"))
                py_lines.append("") # Empty line after cell
                
        return "\n".join(py_lines)
    except json.JSONDecodeError:
        # Not a JSON file, so it's likely a raw Python script
        return content


class SecurityNodeVisitor(ast.NodeVisitor):
    def __init__(self):
        # Gerçek güvenlik Docker konteynerinin kendisidir (--network none, 512MB RAM, 0.5 CPU).
        # Burada sadece konteyner kaçışına yol açabilecek modüller engellenir.
        # os, sys, json, math gibi standart modüller SERBEST — pandas, numpy, matplotlib bunlara bağımlı.
        self.forbidden_modules = {
            "subprocess", "pty", "socket", "ctypes", "multiprocessing"
        }
        
        # Sadece konteyner dışına erişim sağlayabilecek tehlikeli çağrılar
        self.forbidden_calls = {
            "compile", "globals", "locals"
        }
        
        self.dependencies = set()
        self.stdlib_names = set()
        if hasattr(sys, "stdlib_module_names"):
            self.stdlib_names = sys.stdlib_module_names

    def _track_dependency(self, base_module: str):
        if base_module not in self.forbidden_modules and base_module not in self.stdlib_names:
            if not base_module.startswith("_"):
                self.dependencies.add(base_module)

    def visit_Import(self, node):
        for alias in node.names:
            base_module = alias.name.split('.')[0]
            if base_module in self.forbidden_modules:
                raise ValueError(f"Güvenlik İhlali: İzin verilmeyen modül kullanımı ('{base_module}')")
            self._track_dependency(base_module)
        self.generic_visit(node)

    def visit_ImportFrom(self, node):
        if node.level == 0 and node.module:
            base_module = node.module.split('.')[0]
            if base_module in self.forbidden_modules:
                raise ValueError(f"Güvenlik İhlali: İzin verilmeyen modül kullanımı ('{base_module}')")
            self._track_dependency(base_module)
        self.generic_visit(node)

    def visit_Call(self, node):
        if isinstance(node.func, ast.Name):
            if node.func.id in self.forbidden_calls:
                raise ValueError(f"Güvenlik İhlali: İzin verilmeyen fonksiyon çağrısı ('{node.func.id}')")
        if isinstance(node.func, ast.Name) and node.func.id == "__import__":
             raise ValueError("Güvenlik İhlali: '__import__' kullanımı yasaktır.")
             
        self.generic_visit(node)


def analyze_security(code: str) -> list:
    """
    Parses the Python code into an AST and visits nodes to check for forbidden operations.
    Returns a list of external dependencies.
    Raises ValueError if a security violation is detected.
    """
    try:
        tree = ast.parse(code)
    except SyntaxError as e:
        raise ValueError(f"Sözdizimi Hatası (SyntaxError): {str(e)}")
        
    visitor = SecurityNodeVisitor()
    visitor.visit(tree)
    return list(visitor.dependencies)


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')

    if len(sys.argv) < 2:
        print(json.dumps({"code": "", "dependencies": []}))
        sys.exit(0)
        
    file_path = sys.argv[1]
    
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            raw_input = f.read()
    except Exception as e:
        print(f"Bilinmeyen Hata: Dosya okunamadı: {str(e)}", file=sys.stderr)
        sys.exit(1)
    
    if not raw_input.strip():
        print(json.dumps({"code": "", "dependencies": []}))
        sys.exit(0)

    try:
        python_code = parse_ipynb_to_py(raw_input)
        dependencies = analyze_security(python_code)
        
        result = {
            "code": python_code,
            "dependencies": dependencies
        }
        print(json.dumps(result, ensure_ascii=False))
        sys.exit(0)
    except ValueError as ve:
        print(str(ve), file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"Bilinmeyen Hata: {str(e)}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
