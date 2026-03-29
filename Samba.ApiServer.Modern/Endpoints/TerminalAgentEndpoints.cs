using Microsoft.AspNetCore.Mvc;
using Samba.ApiServer.Modern.Contracts;
using Samba.ApiServer.Modern.Services;

namespace Samba.ApiServer.Modern.Endpoints;

public static class TerminalAgentEndpoints
{
    public static void MapTerminalAgentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v2/terminal-agent")
            .WithTags("TerminalAgent")
            .WithName("TerminalAgent");

        group.MapPost("/heartbeats", UpsertHeartbeat)
            .WithName("UpsertTerminalHeartbeat")
            .WithSummary("Receive heartbeat from terminal agent")
            .Accepts<TerminalHeartbeatRequest>("application/json")
            .Produces<TerminalHeartbeatDto>(StatusCodes.Status202Accepted);

        group.MapGet("/heartbeats", ListHeartbeats)
            .WithName("ListTerminalHeartbeats")
            .WithSummary("List latest terminal heartbeat statuses")
            .Produces<IReadOnlyList<TerminalHeartbeatDto>>(StatusCodes.Status200OK);
    }

    private static IResult UpsertHeartbeat(
        [FromBody] TerminalHeartbeatRequest request,
        [FromServices] ITerminalAgentService terminalAgentService,
        [FromServices] ILogger<Program> logger)
    {
        if (string.IsNullOrWhiteSpace(request.TerminalId))
        {
            return Results.BadRequest(new ErrorResponse(
                Error: "ValidationError",
                Message: "terminalId is required"
            ));
        }

        var heartbeat = terminalAgentService.UpsertHeartbeat(request);
        logger.LogInformation("Terminal heartbeat received: {TerminalId} ({StationName})", heartbeat.TerminalId, heartbeat.StationName);
        return Results.Accepted($"/api/v2/terminal-agent/heartbeats/{heartbeat.TerminalId}", heartbeat);
    }

    private static IResult ListHeartbeats(
        [FromServices] ITerminalAgentService terminalAgentService)
    {
        return Results.Ok(terminalAgentService.ListHeartbeats());
    }
}
