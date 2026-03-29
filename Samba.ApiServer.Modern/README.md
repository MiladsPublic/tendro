# Samba.ApiServer.Modern

Parallel modern API host for SambaPOS migration.

## Purpose

This project is the .NET 10 ASP.NET Core host that will gradually replace the legacy self-hosted Web API in `Samba.ApiServer`.

## Run

```bash
dotnet run --project Samba.ApiServer.Modern/Samba.ApiServer.Modern.csproj
```

## Initial endpoints

- `GET /health`
- `GET /api/v2/system/info`

## Notes

- This project is intentionally isolated from legacy MEF/WPF startup.
- Next implementation slices should add auth (`/api/v2/auth/*`) and ticket command/query endpoints.
