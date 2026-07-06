#!/usr/bin/env node

const signalR = require("@microsoft/signalr");
const chokidar = require("chokidar");
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");
const readline = require("readline");

// ── K-5 Fix: Token --token argümanından DEĞİL env var'dan okunuyor ──────────
// CLI argümanında token → ps aux ile tüm sistem kullanıcıları görebilir (güvensiz).
// Ortam değişkeni kullanımı: export CURRERE_SYNC_TOKEN=abc123 && currere-cli connect
let token = process.env.CURRERE_SYNC_TOKEN || null;

async function resolveToken() {
  if (token) return token;

  // Env var yoksa etkileşimli readline ile sor
  return new Promise((resolve) => {
    const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
    process.stdout.write("\x1b[33m[Auth] CURRERE_SYNC_TOKEN env var bulunamadı.\x1b[0m\n");
    rl.question("Sync Token: ", (answer) => {
      rl.close();
      resolve(answer.trim());
    });
  });
}

const API_URL = process.env.CURRERE_API_URL || "http://localhost:5279/syncHub";
let workspaceId = null;

// ── K-4 Fix: isUpdatingFromServer + setTimeout KALDIRILDI ──────────────────
// Eski yöntem: global boolean + 100ms timeout → timing'e bağlı race condition.
// Yeni yöntem: sunucudan yazılan her dosyanın MD5 hash'i bir Map'te saklanır.
// Chokidar tetiklendiğinde disk içeriği hash'i ile karşılaştırılır;
// eşleşiyorsa (biz yazdık) → DROP ET. Değişikse → gerçek kullanıcı değişikliği → gönder.
const serverWrittenHashes = new Map(); // fileName → MD5 hash

function computeHash(content) {
  return crypto.createHash("md5").update(content, "utf8").digest("hex");
}

// SignalR Bağlantısı
const connection = new signalR.HubConnectionBuilder()
  .withUrl(API_URL)
  .withAutomaticReconnect([0, 2000, 5000, 10000]) // 4 kez yeniden deneme
  .build();

// ── D-2 Fix: Sunucu restart / Token süresi dolması için Graceful Fallback ───
// Eski durum: onclose hiç handle edilmiyordu → süreç arka planda 'zombie' kalıyordu.
// Yeni durum: Bağlantı kapandığında neden kapandığı analiz edilip kullanıcı bilgilendiriliyor.
connection.onclose((error) => {
  if (!error) {
    // Sunucu tarafından planlı kapatma (graceful shutdown)
    console.log("\x1b[33m[Sync] Bağlantı sunucu tarafından kapatıldı.\x1b[0m");
    process.exit(0);
    return;
  }

  const msg = error?.message || "";
  const isAuthError = msg.includes("401") || msg.includes("Unauthorized") || msg.includes("403");
  const isTokenExpired = msg.includes("token") || msg.includes("Token");

  if (isAuthError || isTokenExpired) {
    console.error(
      "\x1b[31m[Auth] Token süresi dolmuş veya geçersiz.\n" +
      "Yeni bir token alıp CURRERE_SYNC_TOKEN env var'ı güncelleyin ve tekrar bağlanın.\x1b[0m"
    );
  } else {
    console.error(
      "\x1b[31m[Sync] Sunucu bağlantısı koptu veya token süresi doldu.\n" +
      "Lütfen yeni bir token alarak tekrar bağlanın:\n" +
      "  CURRERE_SYNC_TOKEN=<yeni-token> currere-cli connect\x1b[0m"
    );
  }
  process.exit(1);
});

// Sunucudan güncelleme geldiğinde
connection.on("ReceiveCodeUpdate", (fileName, content) => {
  console.log(`\x1b[36m[Cloud -> Local] Güncelleme: ${fileName}\x1b[0m`);

  try {
    // K-4 Fix: Yazmadan ÖNCE hash'i kaydet — chokidar bunu görmezden gelecek
    serverWrittenHashes.set(fileName, computeHash(content));
    fs.writeFileSync(path.join(process.cwd(), fileName), content, "utf8");
  } catch (err) {
    console.error("\x1b[31mDosya yazma hatası:\x1b[0m", err.message);
    // Yazma başarısız olduysa hash'i temizle ki kullanıcı değişikliği bloklanmasın
    serverWrittenHashes.delete(fileName);
  }
});

connection.on("JoinedSuccess", (id) => {
  workspaceId = id;
  console.log(`\x1b[32m[Sync Hub] Workspace #${id} kanalına başarıyla girildi.\x1b[0m`);
  console.log("\x1b[33m[Watcher] Dosya değişiklikleri izleniyor...\x1b[0m");
  setupWatcher();
});

connection.on("JoinFailed", (msg) => {
  console.error(`\x1b[31m[Auth Failed] ${msg}\x1b[0m`);
  console.error(
    "\x1b[33mİpucu: Yeni token almak için editor'da Settings > Sync Token bölümüne gidin.\x1b[0m"
  );
  process.exit(1);
});

// ── D-1 Fix: Çift watcher kurulumunu önle (reconnect senaryosu) ─────────────
let watcherSetup = false;

function setupWatcher() {
  if (watcherSetup) return;
  watcherSetup = true;

  const watcher = chokidar.watch(".", {
    ignored: /(^|[\/\\])\..|node_modules|currere-cli/,
    persistent: true
  });

  watcher.on("change", (filePath) => {
    const fileName = path.basename(filePath);

    // K-4 Fix: Disk içeriğini oku ve hash ile karşılaştır
    let currentContent;
    try {
      currentContent = fs.readFileSync(filePath, "utf8");
    } catch (err) {
      console.error("\x1b[31mDosya okuma hatası:\x1b[0m", err.message);
      return;
    }

    const currentHash = computeHash(currentContent);
    const serverHash = serverWrittenHashes.get(fileName);

    if (serverHash !== undefined && serverHash === currentHash) {
      // Sunucudan gelen içerikle aynı → echo, gönderme
      serverWrittenHashes.delete(fileName);
      return;
    }

    console.log(`\x1b[35m[Local -> Cloud] Değişiklik algılandı: ${fileName}\x1b[0m`);
    connection
      .invoke("SendCodeUpdate", workspaceId, fileName, currentContent)
      .catch((err) => console.error("\x1b[31mSenkronizasyon hatası:\x1b[0m", err.message));
  });
}

async function start() {
  token = await resolveToken();

  if (!token) {
    console.error("\x1b[31mHata: Token boş olamaz.\x1b[0m");
    console.log("Kullanım: CURRERE_SYNC_TOKEN=<token> currere-cli connect");
    process.exit(1);
  }

  try {
    await connection.start();
    console.log("\x1b[32m[Currere CLI] Sunucuya başarıyla bağlandı.\x1b[0m");
    await connection.invoke("JoinWorkspace", token);
  } catch (err) {
    console.error("\x1b[31m[Sync Error] Bağlantı kurulamadı:\x1b[0m", err.message);
    process.exit(1);
  }
}

start();
