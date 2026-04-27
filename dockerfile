# Temel olarak hafif bir Python sürümü kullanıyoruz
FROM python:3.11-slim

# Çalışma dizinini bizim C# backend'in beklediği gibi /workspace yapıyoruz
WORKDIR /workspace

# Python'un print() çıktılarını bekletmeden anında terminale yansıtmasını sağlar (Çok kritik!)
ENV PYTHONUNBUFFERED=1

# Senin eski dosyandaki gibi veri bilimi kütüphanelerini de kuralım, lazım olacak.
RUN pip install --no-cache-dir pandas numpy matplotlib

# Backend'deki C# servisi "runner.py" dosyasını "/app/runner.py" konumunda aradığı için
# Onu konteynerın içine kopyalamalıyız.
RUN mkdir -p /app
COPY runner.py /app/runner.py
COPY kernel_repl.py /app/kernel_repl.py