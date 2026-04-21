#!/usr/bin/env node

const signalR = require("@microsoft/signalr");
const chokidar = require("chokidar");
const fs = require("fs");
const path = require("path");

// Parametreleri al
const args = process.argv.slice(2);
const tokenIndex = args.indexOf("--token");
const token = tokenIndex > -1 ? args[tokenIndex + 1] : null;

if (!token) {
  console.error("\x1b[31mHata: Lütfen bir bağlantı token'ı belirtin.\x1b[0m");
  console.log("Kullanım: currere-cli connect --token {TOKEN}");
  process.exit(1);
}

const API_URL = "http://localhost:5279/syncHub";
let workspaceId = null;
let isUpdatingFromServer = false;

// SignalR Bağlantısı
const connection = new signalR.HubConnectionBuilder()
  .withUrl(API_URL)
  .withAutomaticReconnect()
  .build();

async function start() {
  try {
    await connection.start();
    console.log("\x1b[32m[Currere CLI] Sunucuya başarıyla bağlandı.\x1b[0m");

    // Odaya katıl
    await connection.invoke("JoinWorkspace", token);
  } catch (err) {
    console.error("\x1b[31m[Sync Error] Bağlantı kurulamadı:\x1b[0m", err.message);
    process.exit(1);
  }
}

// Sunucudan güncelleme geldiğinde
connection.on("ReceiveCodeUpdate", (fileName, content) => {
  console.log(`\x1b[36m[Cloud -> Local] Güncelleme: ${fileName}\x1b[0m`);
  
  isUpdatingFromServer = true;
  try {
    fs.writeFileSync(path.join(process.cwd(), fileName), content, "utf8");
  } catch (err) {
    console.error("\x1b[31mDosya yazma hatası:\x1b[0m", err.message);
  } finally {
    // Echo oluşmasını engellemek için kısa bir gecikme
    setTimeout(() => {
      isUpdatingFromServer = false;
    }, 100);
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
  process.exit(1);
});

// Yerel Dosya İzleyici
function setupWatcher() {
  const watcher = chokidar.watch(".", {
    ignored: /(^|[\/\\])\..|node_modules|currere-cli/, // .git, node_modules vb. yoksay
    persistent: true
  });

  watcher.on("change", (filePath) => {
    if (isUpdatingFromServer) return;

    // Sadece .py ve .csv gibi önemli dosyaları senkronize et (veya her şeyi)
    const fileName = path.basename(filePath);
    console.log(`\x1b[35m[Local -> Cloud] Değişiklik algılandı: ${fileName}\x1b[0m`);

    try {
      const content = fs.readFileSync(filePath, "utf8");
      connection.invoke("SendCodeUpdate", workspaceId, fileName, content);
    } catch (err) {
      console.error("\x1b[31mDosya okuma hatası:\x1b[0m", err.message);
    }
  });
}

start();
