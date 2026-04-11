# ServMon

ServMon is a configurable service and endpoint monitor with a background agent and web dashboard for status and alerting.

## Project layout

- `ServMon.sln`: modern/default solution (`Console/ServMon`, `WebApp`, `Shared/WCMS.Common`, `Shared/WCMS.Common.Tests`)
- `legacy/ServMon.Legacy.sln`: legacy solution (`legacy/ServMonWebCore`, `legacy/ServMonWeb`, `legacy/ServMonWeb/ServMonWeb.Tests`)
- `Console/ServMon`: monitoring agent (`net8.0`)
- `WebApp`: ASP.NET Core MVC UI (`net8.0`, branded `ServMon`; project file `ServMonWeb.csproj`)
- `legacy/ServMonWebCore`: older ASP.NET Core MVC UI (`net8.0`, legacy)
- `legacy/ServMonWeb`: legacy ASP.NET MVC 5 app (`.NET Framework 4.8.1`)
- `legacy/ServMonWeb/ServMonWeb.Tests`: legacy MVC test project (`.NET Framework 4.8.1`)

## Migration and validation (.NET 10 + cross-platform)

Status snapshot date: **2026-04-12**.

- Active `net8.0` projects build successfully using .NET SDK `10.0.103`.
- On macOS (arm64), the modern/default `ServMon.sln` builds successfully.
- Legacy projects are isolated in `legacy/ServMon.Legacy.sln` and remain separate from the default modern build path.
- Cross-platform runtime support is **not fully safe yet** because some code paths remain Windows-specific.
- Full migration details, blockers, validation commands, gates, and rollback notes: [docs/DOTNET10_CROSS_PLATFORM_CHECKLIST.md](docs/DOTNET10_CROSS_PLATFORM_CHECKLIST.md).

## Quickstart (default provider)

```bash
dotnet build WebApp/ServMonWeb.csproj -c Release
dotnet run --project WebApp/ServMonWeb.csproj
```

## Database provider setup (MSSQL + PostgreSQL)

`WebApp` (brand: `ServMon`, technical project: `ServMonWeb`) and legacy `legacy/ServMonWebCore` support two EF Core providers:

- `Postgres` (default)
- `SqlServer`

Configuration keys:

- `DatabaseProvider`:
  - `Postgres` (default; also accepts `PostgreSql`/`Npgsql`)
  - `SqlServer`
- `ConnectionStrings:DefaultConnection` (SQL Server)
- `ConnectionStrings:PostgresConnection` (PostgreSQL)

Example PostgreSQL override (zsh/bash):

```bash
DatabaseProvider=Postgres \
ConnectionStrings__PostgresConnection="Host=localhost;Port=5432;Database=servmon;Username=postgres;Password=postgres" \
dotnet run --project WebApp/ServMonWeb.csproj
```

### Important migration note

Existing checked-in Identity migrations were originally scaffolded for SQL Server.
With PostgreSQL as the default provider, generate/apply provider-specific PostgreSQL migrations before production use.
If SQL Server remains in use, maintain a separate SQL Server migration path as well.
