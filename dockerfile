# Temel olarak hafif bir Python sürümü kullanıyoruz
FROM python:3.11-slim

# Çalışma dizinini bizim C# backend'in beklediği gibi /workspace yapıyoruz
WORKDIR /workspace

# Python'un print() çıktılarını bekletmeden anında terminale yansıtmasını sağlar (Çok kritik!)
ENV PYTHONUNBUFFERED=1

# Senin eski dosyandaki gibi veri bilimi kütüphanelerini de kuralım, lazım olacak.
RUN pip install --no-cache-dir pandas numpy matplotlib

# Kısıtlı kullanıcı oluştur
RUN groupadd -g 1000 currere && \
    useradd -u 1000 -g currere -m -s /bin/bash currere

# /workspace dizinlerini currere kullanıcısına ver
RUN mkdir -p /workspace/data /workspace/output && \
    chown -R currere:currere /workspace

# Backend'deki C# servisi "runner.py" dosyasını "/app/runner.py" konumunda aradığı için
# Onu konteynerın içine kopyalamalıyız.
RUN mkdir -p /app
COPY --chown=currere:currere runner.py /app/runner.py
COPY --chown=currere:currere kernel_repl.py /app/kernel_repl.py

# Python önbellek yazmasını engelle (read-only FS için kritik)
ENV PYTHONDONTWRITEBYTECODE=1

# Root'tan çık
USER currere