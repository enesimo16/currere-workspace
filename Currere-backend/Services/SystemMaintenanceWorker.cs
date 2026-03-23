using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Currere_backend.Data;

namespace Currere_backend.Services
{
    public class SystemMaintenanceWorker : BackgroundService
    {
        private readonly ILogger<SystemMaintenanceWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebHostEnvironment _env;

        public SystemMaintenanceWorker(
            ILogger<SystemMaintenanceWorker> logger,
            IServiceProvider serviceProvider,
            IWebHostEnvironment env)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _env = env;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SystemMaintenanceWorker başlatıldı...");

            while (!stoppingToken.IsCancellationRequested)
            {
                // Acil durumlar ve hemen clean up denemesi için ilk başladığında da bir tur temizler.
                try
                {
                    _logger.LogInformation("Sistem temizliği başlatılıyor (Docker Prune, Files, DB)...");
                    await PerformMaintenanceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "İlk bakım sırasında hata oluştu!");
                }

                // Sistem yük olmayan gecelere (hesapla -> 03:00) kurma
                var now = DateTime.Now;
                var nextRunTime = DateTime.Today.AddDays(1).AddHours(3); // Yarın gece 03:00
                var delay = nextRunTime - now;

                _logger.LogInformation($"Bir sonraki temizlik için bekleniyor... Süre: {delay.TotalHours:F1} saat.");
                
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch(TaskCanceledException)
                {
                    // Uygulama kapanıyor
                    break;
                }
            }
        }

        private async Task PerformMaintenanceAsync(CancellationToken stoppingToken)
        {
            // 1. DOCKER PRUNE (Kalıntıları ve çalışmayan imajları sil)
            try
            {
                var processInfo = new ProcessStartInfo("docker", "system prune -af")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync(stoppingToken);
                    _logger.LogInformation("Docker System Prune başarıyla tamamlandı. Disk alanı açıldı.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Docker prune işlemi başarısız oldu (Docker kurulu olmayabilir veya yetki hatası).");
            }

            // 2. ARTIFACT DOSYA TEMİZLİĞİ (24 Saatten eski klasörler)
            try
            {
                var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var artifactsPath = Path.Combine(webRootPath, "artifacts");

                if (Directory.Exists(artifactsPath))
                {
                    var dirs = Directory.GetDirectories(artifactsPath);
                    foreach (var dir in dirs)
                    {
                        var creationTime = Directory.GetCreationTimeUtc(dir);
                        if (creationTime < DateTime.UtcNow.AddDays(-1))
                        {
                            Directory.Delete(dir, true);
                            _logger.LogInformation($"Süresi dolan eski artifact klasörü silindi: {dir}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Artifact temizliği sırasında bir hata oluştu.");
            }

            // 3. VERİTABANI ARŞİVLEME VE TEMİZLİK (30 Günden eski)
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
                var oldLogs = dbContext.ExperimentLogs.Where(x => x.CreatedAt < thirtyDaysAgo).ToList();

                if (oldLogs.Any())
                {
                    dbContext.ExperimentLogs.RemoveRange(oldLogs);
                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation($"{oldLogs.Count} adet 30 günden eski ExperimentLog kaydı veritabanından kalıcı olarak temizlendi.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Veritabanı arşivleme veya temizleme sırasında hata oluştu.");
            }
        }
    }
}
