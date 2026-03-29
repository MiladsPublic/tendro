using System.Collections.Concurrent;
using Samba.ApiServer.Modern.Contracts;

namespace Samba.ApiServer.Modern.Services;

public interface ITerminalAgentService
{
    TerminalHeartbeatDto UpsertHeartbeat(TerminalHeartbeatRequest request);
    IReadOnlyList<TerminalHeartbeatDto> ListHeartbeats();
    TerminalQueueEventDto EnqueueEvent(TerminalQueueEventRequest request);
    IReadOnlyList<TerminalQueueEventDto> ListQueuedEvents(string terminalId);
    TerminalQueueReplayResultDto ReplayQueuedEvents(string terminalId, int take = 50);
}

public class TerminalAgentService : ITerminalAgentService
{
    private static readonly ConcurrentDictionary<string, TerminalHeartbeatDto> Heartbeats = new();
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<TerminalQueueEventDto>> Queues = new();
    private static long _nextEventId;

    public TerminalHeartbeatDto UpsertHeartbeat(TerminalHeartbeatRequest request)
    {
        var heartbeat = new TerminalHeartbeatDto(
            TerminalId: request.TerminalId,
            StationName: request.StationName,
            Online: request.Online,
            PendingQueueCount: request.PendingQueueCount,
            AgentVersion: request.AgentVersion,
            LastSeenUtc: DateTime.UtcNow);

        Heartbeats.AddOrUpdate(request.TerminalId, heartbeat, (_, _) => heartbeat);
        return heartbeat;
    }

    public IReadOnlyList<TerminalHeartbeatDto> ListHeartbeats()
    {
        return Heartbeats.Values
            .OrderByDescending(x => x.LastSeenUtc)
            .ToList();
    }

    public TerminalQueueEventDto EnqueueEvent(TerminalQueueEventRequest request)
    {
        var queue = Queues.GetOrAdd(request.TerminalId, _ => new ConcurrentQueue<TerminalQueueEventDto>());

        var evt = new TerminalQueueEventDto(
            EventId: Interlocked.Increment(ref _nextEventId),
            TerminalId: request.TerminalId,
            EventType: request.EventType,
            PayloadJson: request.PayloadJson,
            Status: "Queued",
            CreatedAtUtc: DateTime.UtcNow,
            CorrelationId: request.CorrelationId);

        queue.Enqueue(evt);
        return evt;
    }

    public IReadOnlyList<TerminalQueueEventDto> ListQueuedEvents(string terminalId)
    {
        if (!Queues.TryGetValue(terminalId, out var queue))
            return Array.Empty<TerminalQueueEventDto>();

        return queue.ToArray()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList();
    }

    public TerminalQueueReplayResultDto ReplayQueuedEvents(string terminalId, int take = 50)
    {
        if (!Queues.TryGetValue(terminalId, out var queue))
        {
            return new TerminalQueueReplayResultDto(
                TerminalId: terminalId,
                Requested: take,
                Replayed: 0,
                Remaining: 0,
                ExecutedAtUtc: DateTime.UtcNow);
        }

        var replayed = 0;
        var replayStamp = DateTime.UtcNow;
        var replayedItems = new List<TerminalQueueEventDto>();

        while (replayed < take && queue.TryDequeue(out var evt))
        {
            replayedItems.Add(evt with { Status = "Replayed", ReplayedAtUtc = replayStamp });
            replayed++;
        }

        foreach (var item in replayedItems)
        {
            // Replay tracking is represented through this service result for now.
            // Next slice will persist outcomes to durable storage.
        }

        return new TerminalQueueReplayResultDto(
            TerminalId: terminalId,
            Requested: take,
            Replayed: replayed,
            Remaining: queue.Count,
            ExecutedAtUtc: replayStamp);
    }
}
