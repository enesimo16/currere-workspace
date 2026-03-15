FROM python:3.10-slim
WORKDIR /app
COPY runner.py /app/runner.py
RUN pip install --no-cache-dir pandas numpy


# Konteyner çalıştığında bir şey yapmasına gerek yok c# kodu yollayacağız
CMD ["tail", "-f", "/dev/null"]