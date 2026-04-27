using System;
using System.Diagnostics;
using System.Threading;

namespace Currere_backend.Models
{
    /// <summary>
    /// Bir workspace için çalışan Docker kernel prosesinin oturum bilgileri.
    /// Singleton KernelManagerService tarafından yönetilir.
    /// </summary>
    public class KernelSession : IDisposable
    {
        /// <summary>
        /// Docker konteyneri çalıştıran sistem prosesi (docker run -i ...)
        /// </summary>
        public Process DockerProcess { get; set; } = null!;

        /// <summary>
        /// Aynı anda iki hücrenin çalışmasını engelleyen kilit.
        /// Notebook hücreleri sıralı çalışmalıdır.
        /// </summary>
        public SemaphoreSlim ExecutionLock { get; } = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Son aktivite zamanı — idle timeout için kullanılır.
        /// </summary>
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// İlişkili workspace ID
        /// </summary>
        public int WorkspaceId { get; set; }

        /// <summary>
        /// Kernel'ın aktif ve çalışır durumda olup olmadığını kontrol eder.
        /// </summary>
        public bool IsAlive => DockerProcess != null 
                            && !DockerProcess.HasExited 
                            && DockerProcess.StartInfo.RedirectStandardInput;

        public void Dispose()
        {
            try
            {
                ExecutionLock.Dispose();
                if (DockerProcess != null && !DockerProcess.HasExited)
                {
                    DockerProcess.Kill(entireProcessTree: true);
                    DockerProcess.Dispose();
                }
            }
            catch { }
        }
    }
}
