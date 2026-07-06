# ═══════════════════════════════════════════════════════════════════════════════
# Currere Sandbox — God-Mode ML Environment
# ═══════════════════════════════════════════════════════════════════════════════
# Tag: currere-sandbox:god-mode
#
# İçerik:
#   - Temel veri bilimi: numpy, pandas, scipy, matplotlib, seaborn
#   - Makine öğrenmesi: scikit-learn, xgboost, lightgbm, catboost
#   - Deep Learning (CPU): PyTorch, TensorFlow-CPU
#   - NLP / HuggingFace: transformers, datasets, tokenizers, accelerate
#   - Görsel / Yardımcı: Pillow, plotly, bokeh, faker, openpyxl
#
# Build: docker build -t currere-sandbox:god-mode .
# ═══════════════════════════════════════════════════════════════════════════════

FROM python:3.11-slim

# ── Sistem Bağımlılıkları ─────────────────────────────────────────────────────
# libgomp1: XGBoost / LightGBM için OpenMP desteği
# libglib2.0-0: matplotlib / Pillow için
# git: HuggingFace model indirme için (opsiyonel ama gerekli olabilir)
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgomp1 \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender-dev \
    git \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# ── Ortam Değişkenleri ────────────────────────────────────────────────────────
WORKDIR /workspace
ENV PYTHONUNBUFFERED=1
ENV PYTHONDONTWRITEBYTECODE=1
ENV MPLCONFIGDIR=/tmp/matplotlib
ENV MPLBACKEND=Agg

# HuggingFace cache'ini /tmp'ye yönlendir (read-only FS uyumluluğu)
ENV HF_HOME=/tmp/huggingface
ENV TRANSFORMERS_CACHE=/tmp/huggingface/transformers
ENV TORCH_HOME=/tmp/torch

# ── KATMAN 1: Temel Veri Bilimi ───────────────────────────────────────────────
# Bu katman en sık değişmez → Docker cache'de en uzun süre kalır
RUN pip install --no-cache-dir \
    numpy==1.26.4 \
    pandas==2.2.2 \
    scipy==1.13.0 \
    matplotlib==3.9.0 \
    seaborn==0.13.2 \
    statsmodels==0.14.2

# ── KATMAN 2: Makine Öğrenmesi ────────────────────────────────────────────────
RUN pip install --no-cache-dir \
    scikit-learn==1.5.0 \
    xgboost==2.0.3 \
    lightgbm==4.3.0 \
    catboost==1.2.5

# ── KATMAN 3: Deep Learning (CPU-Only) ────────────────────────────────────────
# PyTorch CPU versiyonu — GPU versiyonuna göre çok daha küçük (~750MB vs ~3GB)
RUN pip install --no-cache-dir \
    torch==2.3.0 \
    torchvision==0.18.0 \
    torchaudio==2.3.0 \
    --index-url https://download.pytorch.org/whl/cpu

# TensorFlow CPU — GPU bağımlılığı olmadan
RUN pip install --no-cache-dir \
    tensorflow-cpu==2.16.1

# ── KATMAN 4: NLP / HuggingFace Ekosistemi ────────────────────────────────────
RUN pip install --no-cache-dir \
    transformers==4.41.0 \
    datasets==2.19.1 \
    tokenizers==0.19.1 \
    accelerate==0.30.1 \
    huggingface-hub==0.23.0

# ── KATMAN 5: Görsel, Veri ve Yardımcı Araçlar ───────────────────────────────
RUN pip install --no-cache-dir \
    Pillow==10.3.0 \
    plotly==5.22.0 \
    bokeh==3.4.1 \
    faker==25.1.0 \
    openpyxl==3.1.2 \
    xlrd==2.0.1 \
    pyarrow==16.1.0 \
    sqlalchemy==2.0.30 \
    requests==2.32.2 \
    tqdm==4.66.4

# ── Kullanıcı Güvenliği ───────────────────────────────────────────────────────
# Kısıtlı kullanıcı — root ile çalışmayı engelle
RUN groupadd -g 1000 currere && \
    useradd -u 1000 -g currere -m -s /bin/bash currere

# Workspace dizinlerini oluştur ve sahipliği ver
RUN mkdir -p /workspace/data /workspace/output /app && \
    chown -R currere:currere /workspace /app

# ── Runner Script'lerini Kopyala ──────────────────────────────────────────────
COPY --chown=currere:currere runner.py /app/runner.py
COPY --chown=currere:currere kernel_repl.py /app/kernel_repl.py

# ── Root'tan Çık ──────────────────────────────────────────────────────────────
USER currere