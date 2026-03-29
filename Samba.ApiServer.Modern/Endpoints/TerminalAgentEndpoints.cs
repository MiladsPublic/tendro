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

        group.MapPost("/queues/events", EnqueueQueueEvent)
            .WithName("EnqueueTerminalQueueEvent")
            .WithSummary("Enqueue offline terminal event for replay")
            .Accepts<TerminalQueueEventRequest>("application/json")
            .Produces<TerminalQueueEventDto>(StatusCodes.Status202Accepted)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapGet("/queues/{terminalId}/events", ListQueuedEvents)
            .WithName("ListTerminalQueuedEvents")
            .WithSummary("List pending queued events for terminal")
            .Produces<IReadOnlyList<TerminalQueueEventDto>>(StatusCodes.Status200OK);

        group.MapPost("/queues/{terminalId}/replay", ReplayQueuedEvents)
            .WithName("ReplayTerminalQueuedEvents")
            .WithSummary("Replay queued events for terminal")
            .Produces<TerminalQueueReplayResultDto>(StatusCodes.Status200OK);
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

    private static IResult EnqueueQueueEvent(
        [FromBody] TerminalQueueEventRequest request,
        [FromServices] ITerminalAgentService terminalAgentService,
        [FromServices] ILogger<Program> logger)
    {
        if (string.IsNullOrWhiteSpace(request.TerminalId) || string.IsNullOrWhiteSpace(request.EventType))
        {
            return Results.BadRequest(new ErrorResponse(
                Error: "ValidationError",
                Message: "terminalId and eventType are required"
            ));
        }

        var evt = terminalAgentService.EnqueueEvent(request);
        logger.LogInformation("Queued terminal event {EventId} for {TerminalId}", evt.EventId, evt.TerminalId);
        return Results.Accepted($"/api/v2/terminal-agent/queues/{evt.TerminalId}/events/{evt.EventId}", evt);
    }

    private static IResult ListQueuedEvents(
        [FromRoute] string terminalId,
        [FromServices] ITerminalAgentService terminalAgentService)
    {
        return Results.Ok(terminalAgentService.ListQueuedEvents(terminalId));
    }

    private static IResult ReplayQueuedEvents(
        [FromRoute] string terminalId,
        [FromQuery] int take,
        [FromServices] ITerminalAgentService terminalAgentService,
        [FromServices] ILogger<Program> logger)
    {
        var batchSize = take > 0 ? take : 50;
        var result = terminalAgentService.ReplayQueuedEvents(terminalId, batchSize);
        logger.LogInformation("Replayed {Count} queued terminal events for {TerminalId}", result.Replayed, terminalId);
        return Results.Ok(result);
    }
}
