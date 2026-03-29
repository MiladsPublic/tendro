using System.Collections.Concurrent;
using Samba.ApiServer.Modern.Contracts;

namespace Samba.ApiServer.Modern.Services;

public interface ITerminalAgentService
{
    TerminalHeartbeatDto UpsertHeartbeat(TerminalHeartbeatRequest request);
    IReadOnlyList<TerminalHeartbeatDto> ListHeartbeats();
}

public class TerminalAgentService : ITerminalAgentService
{
    private static readonly ConcurrentDictionary<string, TerminalHeartbeatDto> Heartbeats = new();

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
}
