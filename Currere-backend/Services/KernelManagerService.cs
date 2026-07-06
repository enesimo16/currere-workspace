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
            // God-Mode: ML kütüphaneleri için 4GB RAM ve 2 CPU
            args.Append("--memory 4g --cpus 2.0 ");
            // tmpfs boyutları artırıldı: ML modelleri geçici dosya yazar
            args.Append("--user 1000:1000 --read-only --tmpfs /tmp:rw,noexec,nosuid,size=512m --tmpfs /workspace:rw,noexec,nosuid,size=256m,uid=1000,gid=1000 ");
            args.Append("--security-opt no-new-privileges --cap-drop ALL ");
            // pids-limit artırıldı: PyTorch multiprocessing dataloader spawn edebilir
            args.Append("--pids-limit 128 ");
            args.Append("--ipc none ");
            args.Append($"--name currere-kernel-{workspaceId} ");
            args.Append("-w /workspace ");
            args.Append($"-v \"{dockerWorkspaceBind}:/workspace/data:ro\" ");
            args.Append($"-v \"{dockerOutputBind}:/workspace/output:rw\" ");
            args.Append("-e PYTHONUNBUFFERED=1 ");
            // HuggingFace ve Torch cache'lerini /tmp'ye yönlendir (read-only FS uyumu)
            args.Append("-e HF_HOME=/tmp/huggingface ");
            args.Append("-e TRANSFORMERS_CACHE=/tmp/huggingface/transformers ");
            args.Append("-e TORCH_HOME=/tmp/torch ");
            args.Append("currere-sandbox:god-mode ");
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
                        _logger.LogWarning("[Kernel] WorkspaceId: {WsId} — kernel HAZIR \u2713", workspaceId);
                    }
                }
                catch
                {
                    _logger.LogWarning("[Kernel] WorkspaceId: {WsId} — ready sinyali alindı ama parse edilemedi: {Line}", workspaceId, readyLine);
                }
            }
            else
            {
                // O-3 Fix: Zombi process öldürülüyor ve exception firlatılıyor.
                // Eski kod: olu process'i session olarak döndürüyordu → sonraki her çagrıda timeout.
                _logger.LogError("[Kernel] WorkspaceId: {WsId} — 30 saniyede ready sinyali GELMEDI! Zombi process öldürülüyor.", workspaceId);

                try { process.Kill(entireProcessTree: true); } catch { }
                try { process.Dispose(); } catch { }

                // Docker container ad calismis olabilir, zorla temizle
                try
                {
                    var cleanupProc = new Process
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
                    cleanupProc.Start();
                    cleanupProc.WaitForExit(3000);
                }
                catch { }

                throw new InvalidOperationException(
                    $"[Kernel] WorkspaceId: {workspaceId} — Kernel 30 saniye içinde hazır olmadi. " +
                    "Docker imajının dogřru kurulduğundan emin olun (currere-sandbox:god-mode).");
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
            // K-3 Fix: Kilit almadan ÖNCE ölü session kontrolü yap.
            // Eski kod: kilitli scope içinde yeni WaitAsync → çifte kilit → Deadlock.
            // Yeni davranış: ölü session kilit dışında temizlenir, sağlıklı session üzerinde
            // TEK bir WaitAsync yapılır. Disposed objeye Release riski ortadan kalktı.
            var session = await GetOrCreateSessionAsync(workspaceId);

            if (!session.IsAlive)
            {
                _logger.LogWarning("[Kernel] WorkspaceId: {WsId} — process ölü, kilit dışında yeniden başlatılıyor", workspaceId);
                _sessions.TryRemove(workspaceId, out _);
                try { session.Dispose(); } catch { /* Dispose hatası kritik değil */ }
                // Yeni, sağlıklı session al — kilit bu session üzerinde alınacak
                session = await GetOrCreateSessionAsync(workspaceId);
            }

            // Tek ve temiz session üzerinde kilit al
            await session.ExecutionLock.WaitAsync();
            try
            {
                // Kilit alındıktan sonra tekrar kontrol (başka thread aynı anda çökertmiş olabilir)
                if (!session.IsAlive)
                {
                    return new KernelExecutionResult
                    {
                        Success = false,
                        ErrorType = "KernelError",
                        Message = "Kernel beklenmedik şekilde çöktü. 'Restart' butonuna basın."
                    };
                }

                session.LastActivityAt = DateTime.UtcNow;
                // D-5 Fix: Reaper koruması — hücre çalışırken IsExecuting=true → Reaper bu session'ı atlayacak
                session.IsExecuting = true;

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
                // D-5 Fix: Hücre bitti (başarılı, hatalı veya timeout) → Reaper'a izin ver
                session.IsExecuting = false;
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
                .Where(kvp =>
                    (now - kvp.Value.LastActivityAt) > idleTimeout
                    && !kvp.Value.IsExecuting) // D-5 Fix: Aktif hücre çalıştıran kernel'lar atlansın
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
