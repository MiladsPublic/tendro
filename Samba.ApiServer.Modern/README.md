# Samba.ApiServer.Modern

Parallel modern .NET 10 ASP.NET Core API host for SambaPOS migration.

## Purpose

This project is the Phase 1+ modern API host that will gradually replace the legacy self-hosted Web API in `Samba.ApiServer`. Built with strangler pattern: new clients use modern endpoints, legacy clients remain supported until migration is complete.

## Architecture

### Phase 1: Foundation (current)

✓ Structured logging with JSON console output  
✓ Request correlation IDs for traceability  
✓ Standardized error responses (RFC 7807)  
✓ Health checks with component metadata  
✓ Basic authentication framework  
✓ CORS support for web clients  
✓ API versioning (v2 routes)  
✓ Request/response logging middleware  
✓ OpenAPI/Swagger documentation  

### Phase 2: Core Workflows (planned)

- Ticket lifecycle endpoints (/api/v2/tickets/*)
- Order management endpoints (/api/v2/orders/*)
- Payment processing endpoints (/api/v2/payments/*)
- Database persistence layer integration

### Phase 3+: Hardware & Offline Support

- Terminal agent protocol
- Offline queue management
- Hardware (printer, cash drawer, display) integration

## Endpoints (Phase 1)

### System & Health
- `GET /api/v2/system/health` - Health status with components
- `GET /api/v2/system/info` - System version and environment
- `GET /api/v2/system/health/ready` - Kubernetes readiness probe
- `GET /api/v2/system/health/live` - Kubernetes liveness probe
- `GET /api/v2/system/metrics` - Performance metrics

### Authentication
- `POST /api/v2/auth/login` - Authenticate and get bearer token
- `POST /api/v2/auth/logout` - Invalidate user session

### Documentation
- `GET /api-docs` - Interactive Swagger UI
- `GET /swagger/v2/swagger.json` - OpenAPI specification

## Run Locally

```bash
# Development mode with hot reload
dotnet run --project Samba.ApiServer.Modern/Samba.ApiServer.Modern.csproj --environment Development

# Production mode
dotnet run --project Samba.ApiServer.Modern/Samba.ApiServer.Modern.csproj --environment Production
```

Server will listen on `https://localhost:5180` by default.

### Verify Health

```bash
# Quick health check
curl https://localhost:5180/api/v2/system/health

# System info
curl https://localhost:5180/api/v2/system/info

# Bearer token login (Phase 1 hardcoded: admin/admin)
curl -X POST https://localhost:5180/api/v2/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin"}'
```

## Configuration

### appsettings.json

```json
{
  "Logging": { /* Structured JSON logging */ },
  "ApiSettings": {
    "Version": "2.0.0",
    "EnableSwagger": true,
    "RequestLoggingEnabled": true
  },
  "HealthChecks": {
    "HealthCheckPath": "/health",
    "ReadinessPath": "/health/ready",
    "LivenessPath": "/health/live"
  }
}
```

### Logging Levels

| Component | Level | Notes |
|-----------|-------|-------|
| Default | Information | General request/response log |
| AspNetCore | Warning | Framework logs (verbose at debug) |
| Samba.ApiServer.Modern | Debug | Detailed API execution flow |

Enable detailed logging per environment in `appsettings.{Environment}.json`.

## Design Patterns

### Structured Logging
Every log entry includes context:
- CorrelationId: Trace request across services
- RequestId: Local trace identifier
- Scopes: Nested context (user, tenant, operation)

### Standardized Errors
All errors follow RFC 7807 Problem Details format:
```json
{
  "type": "https://example.com/errors/validation-error",
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "POST /api/v2/tickets",
  "correlationId": "f7cf3ef-637d-45c6f-b3fa",
  "errors": {
    "Amount": ["Must be greater than 0"]
  }
}
```

### Request Correlation
- Inbound `X-Correlation-Id` header preserved
- Generated if missing (GUID format)
- Propagated to response headers
- Logged with all events for service-to-service tracing

### Health Checks
- **Live**: Basic heartbeat (process is running)
- **Ready**: Dependencies OK (can accept traffic)
- Components reported separately (database, external services)

## Migration Notes

### Phase 1 Limitations
- Authentication is in-memory hardcoded (admin/admin)
- No database integration yet
- Health checks are placeholder

### Phase 2 Integration Points
- Will reference Samba.Domain.Models.Tickets.Ticket
- Will use Samba.Presentation.Services.ITicketService
- Will connect to legacy database via EF Core
- Will publish legacy events (RuleEventNames)

### Backwards Compatibility
- Legacy `/api/*` routes remain unchanged
- Modern `/api/v2/*` routes exist in parallel
- Clients migrate at their own pace
- No big-bang cutover required

## Building & Testing

```bash
# Build
dotnet build Samba.ApiServer.Modern/Samba.ApiServer.Modern.csproj

# Run tests
dotnet test Samba.Services.Tests --filter "ServerHosting"

# Package for deployment
dotnet publish Samba.ApiServer.Modern/Samba.ApiServer.Modern.csproj -c Release -o ./publish

# Docker (planned Phase 2)
docker build -t sambappos-api-modern:2.0.0 .
docker run -p 5180:5180 sambappos-api-modern:2.0.0
```

## References

- [Implementation Plan](../docs/migration/03-implementation-plan.md)
- [API Standards](../docs/migration/04-reference-implementation.md)
- [Phase 1 Foundation Architecture](../docs/migration/02-target-architecture.md)
