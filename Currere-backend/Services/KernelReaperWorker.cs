using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Currere_backend.Services
{
    /// <summary>
    /// Zombi Avcısı: Her 2 dakikada bir uyanarak 15 dakikadan uzun süredir
    /// hareketsiz kalan (idle) kernel session'larını acımasızca öldürür.
    /// Bu sayede sunucu kaynaklarının tükenmesi engellenir.
    /// </summary>
    public class KernelReaperWorker : BackgroundService
    {
        private readonly KernelManagerService _kernelManager;
        private readonly ILogger<KernelReaperWorker> _logger;

        private static readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(15);

        public KernelReaperWorker(KernelManagerService kernelManager, ILogger<KernelReaperWorker> logger)
        {
            _kernelManager = kernelManager;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[KernelReaper] Zombi avcısı başlatıldı. Kontrol aralığı: {Interval}dk, Idle timeout: {Timeout}dk",
                _checkInterval.TotalMinutes, _idleTimeout.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                try
                {
                    ReapIdleSessions();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[KernelReaper] Zombi avı sırasında hata oluştu!");
                }
            }

            _logger.LogInformation("[KernelReaper] Zombi avcısı durduruldu.");
        }

        private void ReapIdleSessions()
        {
            var idleSessions = _kernelManager.GetIdleSessions(_idleTimeout);

            if (idleSessions.Count == 0)
            {
                _logger.LogDebug("[KernelReaper] Hareketsiz kernel bulunamadı.");
                return;
            }

            _logger.LogWarning("[KernelReaper] {Count} adet hareketsiz kernel tespit edildi, öldürülüyor...", idleSessions.Count);

            foreach (var workspaceId in idleSessions)
            {
                try
                {
                    _kernelManager.ForceKillSession(workspaceId);
                    _logger.LogWarning("[KernelReaper] WorkspaceId: {WsId} — zombi kernel öldürüldü ✓", workspaceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[KernelReaper] WorkspaceId: {WsId} — kernel öldürülürken hata!", workspaceId);
                }
            }
        }
    }
}
