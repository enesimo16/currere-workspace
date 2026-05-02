using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Currere_backend.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Currere_backend.Services
{
    /// <summary>
    /// Workspace başına uzun ömürlü Docker kernel konteyneri yöneten Singleton servis.
    /// Her workspace'in kendi "beyni" vardır ve hücreler arası değişkenler hafızada kalır.
    /// Docker.DotNet KULLANILMAZ — tüm iletişim System.Diagnostics.Process üzerinden yapılır.
    /// </summary>
    public class KernelManagerService : IDisposable
    {
        private readonly ConcurrentDictionary<int, KernelSession> _sessions = new();
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<KernelManagerService> _logger;

        public KernelManagerService(IWebHostEnvironment env, ILogger<KernelManagerService> logger)
        {
            _env = env;
            _logger = logger;
        }

        /// <summary>
        /// Belirtilen workspace için aktif kernel oturumunu döndürür.
        /// Yoksa veya ölmüşse yeni bir tane başlatır.
        /// </summary>
        public async Task<KernelSession> GetOrCreateSessionAsync(int workspaceId)
        {
            // Mevcut ve canlı session varsa dön
            if (_sessions.TryGetValue(workspaceId, out var existing) && existing.IsAlive)
            {
                existing.LastActivityAt = DateTime.UtcNow;
                return existing;
            }

            // Ölü session varsa temizle
            if (existing != null)
            {
                _logger.LogWarning("[Kernel] WorkspaceId: {WsId} — ölü session temizleniyor", workspaceId);
                existing.Dispose();
                _sessions.TryRemove(workspaceId, out _);
            }

            // Yeni kernel başlat
            var session = await StartKernelAsync(workspaceId);
            _sessions[workspaceId] = session;
            return session;
        }

        /// <summary>
        /// Yeni bir Docker kernel konteyneri başlatır.
        /// docker run -i --rm --network none -v workspace:/workspace/data:ro currere-sandbox python /app/kernel_repl.py
        /// </summary>
        private async Task<KernelSession> StartKernelAsync(int workspaceId)
        {
            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var hostWorkspacePath = Path.Combine(webRootPath, "workspaces", workspaceId.ToString());

            if (!Directory.Exists(hostWorkspacePath))
                Directory.CreateDirectory(hostWorkspacePath);

            var dockerWorkspaceBind = hostWorkspacePath.Replace("\\", "/");

            // Output klasörü
            var hostOutputPath = Path.Combine(webRootPath, "artifacts", $"kernel_{workspaceId}");
            if (!Directory.Exists(hostOutputPath))
                Directory.CreateDirectory(hostOutputPath);
            var dockerOutputBind = hostOutputPath.Replace("\\", "/");

            var args = new StringBuilder();
            args.Append("run -i --rm --network none ");
            args.Append("--memory 512m --cpus 0.5 ");
            args.Append("--user 1000:1000 --read-only --tmpfs /tmp:rw,noexec,nosuid,size=64m --tmpfs /workspace:rw,noexec,nosuid,size=32m,uid=1000,gid=1000 ");
            args.Append("--security-opt no-new-privileges --cap-drop ALL ");
            args.Append("--pids-limit 50 ");
            args.Append("--ipc none ");
            args.Append($"--name currere-kernel-{workspaceId} ");
            args.Append("-w /workspace ");
            args.Append($"-v \"{dockerWorkspaceBind}:/workspace/data:ro\" ");
            args.Append($"-v \"{dockerOutputBind}:/workspace/output:rw\" ");
            args.Append("-e PYTHONUNBUFFERED=1 ");
            args.Append("currere-sandbox:latest ");
            args.Append("python /app/kernel_repl.py");

            _logger.LogWarning("[Kernel] WorkspaceId: {WsId} — yeni kernel başlatılıyor", workspaceId);
            _logger.LogWarning("[Kernel] docker {Args}", args.ToString().Length > 200 ? args.ToString().Substring(0, 200) + "..." : args.ToString());

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = args.ToString(),
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            process.Start();
            _logger.LogWarning("[Kernel] WorkspaceId: {WsId} — process başlatıldı. PID: {PID}", workspaceId, process.Id);

            // Kernel'ın "ready" sinyalini bekle (maksimum 30 saniye)
            var readyLine = await ReadLineWithTimeoutAsync(process.StandardOutput, TimeSpan.FromSeconds(30));
            
            if (readyLine != null)
            {
                try
                {
                    var readyJson = JsonDocument.Parse(readyLine);
                    if (readyJson.RootElement.GetProperty("type").GetString() == "ready")
                    {
                        _logger.LogWarning("[Kernel] WorkspaceId: {WsId} — kernel HAZIR ✓", workspaceId);
                    }
                }
                catch
                {
                    _logger.LogWarning("[Kernel] WorkspaceId: {WsId} — ready sinyali alındı ama parse edilemedi: {Line}", workspaceId, readyLine);
                }
            }
            else
            {
                _logger.LogError("[Kernel] WorkspaceId: {WsId} — 30 saniyede ready sinyali GELMEDİ!", workspaceId);
            }

            var session = new KernelSession
            {
                DockerProcess = process,
                WorkspaceId = workspaceId,
                LastActivityAt = DateTime.UtcNow
            };

            return session;
        }

        /// <summary>
        /// Belirtilen kernel'a kod gönderir ve sonucu alır.
        /// SemaphoreSlim ile aynı anda tek hücre çalışmasını garanti eder.
        /// </summary>
        public async Task<KernelExecutionResult> ExecuteCellAsync(int workspaceId, string code)
        {
            var session = await GetOrCreateSessionAsync(workspaceId);

            // Aynı anda iki hücre çalışmasını engelle (Notebook hücreleri sıralıdır)
            await session.ExecutionLock.WaitAsync();
            try
            {
                if (!session.IsAlive)
                {
                    // Process ölmüşse yeniden başlat
                    _logger.LogWarning("[Kernel] WorkspaceId: {WsId} — process ölü, yeniden başlatılıyor", workspaceId);
                    
                    var oldSession = session;
                    _sessions.TryRemove(workspaceId, out _);
                    
                    // Yeni session al ve kilidini kap (çünkü finally bloğu bu yeni session'ı release edecek)
                    session = await GetOrCreateSessionAsync(workspaceId);
                    await session.ExecutionLock.WaitAsync();
                    
                    // Eski session'ı dispose et ve kilidini bırak ki bekleyen thread'ler uyansın
                    // Onlar uyanınca da eski session'ın öldüğünü görüp buraya girecekler.
                    oldSession.Dispose();
                    oldSession.ExecutionLock.Release();
                }

                session.LastActivityAt = DateTime.UtcNow;

                // Kodu base64'e çevir
                var codeBytes = new UTF8Encoding(false).GetBytes(code);
                var base64Code = Convert.ToBase64String(codeBytes);

                // JSON komutu oluştur ve stdin'e gönder
                var command = JsonSerializer.Serialize(new { action = "execute", code = base64Code });
                
                await session.DockerProcess.StandardInput.WriteLineAsync(command);
                await session.DockerProcess.StandardInput.FlushAsync();

                _logger.LogInformation("[Kernel] WorkspaceId: {WsId} — hücre gönderildi ({Len} karakter)", workspaceId, code.Length);

                // Stdout'tan JSON cevabı oku (45 saniye timeout)
                var responseLine = await ReadLineWithTimeoutAsync(session.DockerProcess.StandardOutput, TimeSpan.FromSeconds(45));

                if (responseLine == null)
                {
                    return new KernelExecutionResult
                    {
                        Success = false,
                        ErrorType = "TimeoutError",
                        Message = "Hücre 45 saniye içinde yanıt vermedi."
                    };
                }

                // JSON parse
                try
                {
                    var json = JsonDocument.Parse(responseLine);
                    var root = json.RootElement;
                    return new KernelExecutionResult
                    {
                        Success = root.GetProperty("success").GetBoolean(),
                        ErrorType = root.TryGetProperty("error_type", out var et) && et.ValueKind != JsonValueKind.Null ? et.GetString() : null,
                        Message = root.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : ""
                    };
                }
                catch
                {
                    return new KernelExecutionResult
                    {
                        Success = false,
                        ErrorType = "ParseError",
                        Message = $"Kernel yanıtı parse edilemedi: {responseLine}"
                    };
                }
            }
            finally
            {
                session.ExecutionLock.Release();
            }
        }

        /// <summary>
        /// Kernel'ı durdur ve yeniden başlat (hafızayı sıfırlar).
        /// </summary>
        public async Task RestartKernelAsync(int workspaceId)
        {
            if (_sessions.TryRemove(workspaceId, out var session))
            {
                _logger.LogWarning("[Kernel] WorkspaceId: {WsId} — kernel yeniden başlatılıyor (restart)", workspaceId);
                session.Dispose();
            }

            // Eski konteyneri zorla temizle (isim çakışması engellemek için)
            try
            {
                var killProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "docker",
                        Arguments = $"rm -f currere-kernel-{workspaceId}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                killProcess.Start();
                await killProcess.WaitForExitAsync();
            }
            catch { }

            // Yeni session otomatik oluşacak (GetOrCreateSessionAsync ile)
            await GetOrCreateSessionAsync(workspaceId);
            _logger.LogWarning("[Kernel] WorkspaceId: {WsId} — kernel yeniden başlatıldı ✓", workspaceId);
        }

        /// <summary>
        /// Kernel'ın hayatta olup olmadığını kontrol eder.
        /// </summary>
        public bool IsKernelAlive(int workspaceId)
        {
            return _sessions.TryGetValue(workspaceId, out var session) && session.IsAlive;
        }

        /// <summary>
        /// Belirtilen StreamReader'dan timeout süresinde bir satır okur.
        /// </summary>
        private async Task<string?> ReadLineWithTimeoutAsync(StreamReader reader, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                var readTask = reader.ReadLineAsync(cts.Token);
                return await readTask;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Belirtilen idle timeout süresini aşmış session'ların workspace ID'lerini döndürür.
        /// KernelReaperWorker tarafından kullanılır.
        /// </summary>
        public List<int> GetIdleSessions(TimeSpan idleTimeout)
        {
            var now = DateTime.UtcNow;
            return _sessions
                .Where(kvp => (now - kvp.Value.LastActivityAt) > idleTimeout)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// Belirtilen workspace'in kernel session'ını zorla öldürür.
        /// KernelReaperWorker tarafından kullanılır.
        /// </summary>
        public void ForceKillSession(int workspaceId)
        {
            if (_sessions.TryRemove(workspaceId, out var session))
            {
                _logger.LogWarning("[Kernel] WorkspaceId: {WsId} — session zorla öldürüldü (idle timeout)", workspaceId);
                session.Dispose();

                // Eski konteyneri zorla temizle
                try
                {
                    var killProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "docker",
                            Arguments = $"rm -f currere-kernel-{workspaceId}",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                    };
                    killProcess.Start();
                    killProcess.WaitForExit(5000);
                }
                catch { }
            }
        }

        /// <summary>
        /// Uygulama kapanırken tüm kernel'ları temizle.
        /// </summary>
        public void Dispose()
        {
            _logger.LogWarning("[Kernel] Tüm kernel session'ları kapatılıyor...");
            foreach (var kvp in _sessions)
            {
                try { kvp.Value.Dispose(); }
                catch { }
            }
            _sessions.Clear();
        }
    }

    /// <summary>
    /// Kernel hücre çalıştırma sonucu.
    /// </summary>
    public class KernelExecutionResult
    {
        public bool Success { get; set; }
        public string? ErrorType { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
