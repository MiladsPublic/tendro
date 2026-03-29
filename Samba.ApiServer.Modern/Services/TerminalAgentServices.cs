using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Samba.ApiServer.Modern.Contracts;
using Samba.ApiServer.Modern.Data;

namespace Samba.ApiServer.Modern.Services;

public interface ITerminalAgentService
{
    TerminalHeartbeatDto UpsertHeartbeat(TerminalHeartbeatRequest request);
    IReadOnlyList<TerminalHeartbeatDto> ListHeartbeats();
    Task<TerminalQueueEventDto> EnqueueEventAsync(TerminalQueueEventRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TerminalQueueEventDto>> ListQueuedEventsAsync(string terminalId, CancellationToken ct = default);
    Task<TerminalQueueReplayResultDto> ReplayQueuedEventsAsync(string terminalId, int take = 50, CancellationToken ct = default);
}

public class TerminalAgentService : ITerminalAgentService
{
    private readonly SambaDbContext _dbContext;
    private static readonly ConcurrentDictionary<string, TerminalHeartbeatDto> Heartbeats = new();

    public TerminalAgentService(SambaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

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

    public async Task<TerminalQueueEventDto> EnqueueEventAsync(TerminalQueueEventRequest request, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            var existing = await _dbContext.TerminalQueueEvents
                .AsNoTracking()
                .OrderByDescending(e => e.CreatedAtUtc)
                .FirstOrDefaultAsync(
                    e => e.TerminalId == request.TerminalId &&
                         e.CorrelationId == request.CorrelationId,
                    ct);

            if (existing != null)
            {
                return new TerminalQueueEventDto(
                    EventId: existing.Id,
                    TerminalId: existing.TerminalId,
                    EventType: existing.EventType,
                    PayloadJson: existing.PayloadJson,
                    Status: "Conflict",
                    CreatedAtUtc: existing.CreatedAtUtc,
                    ReplayedAtUtc: existing.ReplayedAtUtc,
                    CorrelationId: existing.CorrelationId,
                    ReplayOutcome: existing.ReplayOutcome,
                    ConflictReason: "DuplicateCorrelationId");
            }
        }

        var entity = new TerminalQueueEventEntity
        {
            TerminalId = request.TerminalId,
            EventType = request.EventType,
            PayloadJson = request.PayloadJson,
            Status = "Queued",
            CorrelationId = request.CorrelationId,
            CreatedAtUtc = DateTime.UtcNow,
        };

        _dbContext.TerminalQueueEvents.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        return new TerminalQueueEventDto(
            EventId: entity.Id,
            TerminalId: entity.TerminalId,
            EventType: entity.EventType,
            PayloadJson: entity.PayloadJson,
            Status: entity.Status,
            CreatedAtUtc: entity.CreatedAtUtc,
            ReplayedAtUtc: entity.ReplayedAtUtc,
            CorrelationId: entity.CorrelationId,
            ReplayOutcome: entity.ReplayOutcome,
            ConflictReason: entity.ConflictReason);
    }

    public async Task<IReadOnlyList<TerminalQueueEventDto>> ListQueuedEventsAsync(string terminalId, CancellationToken ct = default)
    {
        var items = await _dbContext.TerminalQueueEvents
            .AsNoTracking()
            .Where(x => x.TerminalId == terminalId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        return items.Select(x => new TerminalQueueEventDto(
            EventId: x.Id,
            TerminalId: x.TerminalId,
            EventType: x.EventType,
            PayloadJson: x.PayloadJson,
            Status: x.Status,
            CreatedAtUtc: x.CreatedAtUtc,
            ReplayedAtUtc: x.ReplayedAtUtc,
            CorrelationId: x.CorrelationId,
            ReplayOutcome: x.ReplayOutcome,
            ConflictReason: x.ConflictReason)).ToList();
    }

    public async Task<TerminalQueueReplayResultDto> ReplayQueuedEventsAsync(string terminalId, int take = 50, CancellationToken ct = default)
    {
        var queued = await _dbContext.TerminalQueueEvents
            .Where(x => x.TerminalId == terminalId && x.Status == "Queued")
            .OrderBy(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);

        if (queued.Count == 0)
        {
            return new TerminalQueueReplayResultDto(
                TerminalId: terminalId,
                Requested: take,
                Replayed: 0,
                Remaining: 0,
                ExecutedAtUtc: DateTime.UtcNow);
        }

        var replayStamp = DateTime.UtcNow;
        foreach (var evt in queued)
        {
            evt.Status = "Replayed";
            evt.ReplayedAtUtc = replayStamp;
            evt.ReplayOutcome = "Applied";
        }

        await _dbContext.SaveChangesAsync(ct);

        var remaining = await _dbContext.TerminalQueueEvents
            .CountAsync(x => x.TerminalId == terminalId && x.Status == "Queued", ct);

        return new TerminalQueueReplayResultDto(
            TerminalId: terminalId,
            Requested: take,
            Replayed: queued.Count,
            Remaining: remaining,
            ExecutedAtUtc: replayStamp);
    }
}
