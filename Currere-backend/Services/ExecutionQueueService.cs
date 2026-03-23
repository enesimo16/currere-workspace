using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Threading.Tasks;
using Currere_backend.Models;

namespace Currere_backend.Services
{
    public interface IExecutionQueueService
    {
        Task EnqueueJobAsync(ExecutionJob job);
        ValueTask<ExecutionJob> DequeueJobAsync(System.Threading.CancellationToken cancellationToken);
        ExecutionJob? GetJobStatus(string jobId);
        void UpdateJob(ExecutionJob job);
    }

    public class ExecutionQueueService : IExecutionQueueService
    {
        private readonly Channel<ExecutionJob> _queue;
        private readonly ConcurrentDictionary<string, ExecutionJob> _jobStatuses;

        public ExecutionQueueService()
        {
            // limitsiz queue
            var options = new UnboundedChannelOptions { SingleReader = true };
            _queue = Channel.CreateUnbounded<ExecutionJob>(options);
            _jobStatuses = new ConcurrentDictionary<string, ExecutionJob>();
        }

        public async Task EnqueueJobAsync(ExecutionJob job)
        {
            _jobStatuses[job.JobId] = job;
            await _queue.Writer.WriteAsync(job);
        }

        public async ValueTask<ExecutionJob> DequeueJobAsync(System.Threading.CancellationToken cancellationToken)
        {
            return await _queue.Reader.ReadAsync(cancellationToken);
        }

        public ExecutionJob? GetJobStatus(string jobId)
        {
            if (_jobStatuses.TryGetValue(jobId, out var job))
            {
                return job;
            }
            return null;
        }

        public void UpdateJob(ExecutionJob job)
        {
            _jobStatuses[job.JobId] = job;
        }
    }
}
