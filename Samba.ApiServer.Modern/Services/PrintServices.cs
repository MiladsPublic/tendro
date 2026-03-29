using System.Collections.Concurrent;
using System.Threading;
using Samba.ApiServer.Modern.Contracts;

namespace Samba.ApiServer.Modern.Services;

public interface IPrintService
{
    Task<PrintJobDto> QueueTicketReprintAsync(ReprintTicketRequest request, CancellationToken ct = default);
}

public class PrintService : IPrintService
{
    private static long _nextJobId;
    private static readonly ConcurrentQueue<PrintJobDto> Queue = new();

    public Task<PrintJobDto> QueueTicketReprintAsync(ReprintTicketRequest request, CancellationToken ct = default)
    {
        var job = new PrintJobDto(
            JobId: Interlocked.Increment(ref _nextJobId),
            TicketId: request.TicketId,
            JobType: "TicketReprint",
            Status: "Queued",
            CreatedAtUtc: DateTime.UtcNow,
            Reason: request.Reason,
            RequestedBy: request.RequestedBy);

        Queue.Enqueue(job);
        return Task.FromResult(job);
    }
}
