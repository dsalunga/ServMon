# ServMon

ServMon is a configurable service and endpoint monitor with a background agent and web dashboard for status and alerting.

## Project layout

- `ServMon.sln`: modern/default solution (`Console/ServMon`, `WebApp`, `Shared/WCMS.Common`, `Shared/WCMS.Common.Tests`)
- `legacy/ServMon.Legacy.sln`: legacy solution (`legacy/ServMonWebCore`, `legacy/ServMonWeb`, `legacy/ServMonWeb/ServMonWeb.Tests`)
- `Console/ServMon`: monitoring agent (`net10.0`, cross-platform)
- `WebApp`: ASP.NET Core MVC UI (`net10.0`, branded `ServMon`; project file `ServMonWeb.csproj`)
- `Shared/WCMS.Common`: shared utilities (`net10.0`)
- `Shared/WCMS.Common.Tests`: unit tests (`net10.0`)
- `legacy/ServMonWebCore`: older ASP.NET Core MVC UI (`net8.0`, legacy, Windows-only)
- `legacy/ServMonWeb`: legacy ASP.NET MVC 5 app (`.NET Framework 4.8.1`, Windows-only)
- `legacy/ServMonWeb/ServMonWeb.Tests`: legacy MVC test project (`.NET Framework 4.8.1`, Windows-only)

> **Note:** Legacy `.NET Framework` projects under `legacy/` are Windows-only and are not included in the default `ServMon.sln`. They remain as read-only references for audit/history purposes.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (pinned via `global.json`)
- PostgreSQL (default) or SQL Server for the web app

## Quickstart (cross-platform)

```bash
# Build everything
dotnet build ServMon.sln -c Release

# Run the web app
dotnet run --project WebApp/ServMonWeb.csproj

# Run the console agent
dotnet run --project Console/ServMon/ServMon.csproj

# Run tests
dotnet test ServMon.sln
```

## Configuration

All configuration paths use relative paths by default and work cross-platform (Windows, macOS, Linux).

Key settings in `WebApp/appsettings.json`:

| Key | Description |
|-----|-------------|
| `ServMon:ServicesJsonPath` | Path to the agent's services.json output |
| `ServMon:ConfigPath` | Path to the agent's config.xml |
| `ServMon:ExecutablePath` | Path to the agent executable (no `.exe` extension) |
| `ServMon:ProcessName` | Process name for agent lifecycle management |
| `ServMon:PidFilePath` | PID file used to manage only the web-started agent process |

Override any setting via environment variables:

```bash
export appSettings__ServMon__ExecutablePath="/custom/path/to/ServMon"
```

For legacy Windows config examples, see [docs/migration/legacy-config-examples.md](docs/migration/legacy-config-examples.md).

### One-time admin bootstrap

`WebApp` enforces an `Admin` role for process-control and config-edit endpoints.

- Keep `BootstrapAdmin:Enabled` as `false` by default.
- For first-time setup only, set:
  - `BootstrapAdmin__Enabled=true`
  - `BootstrapAdmin__Email=<admin-email>`
  - `BootstrapAdmin__Password=<strong-password>`
- Start the app once to seed the admin user and role.
- Immediately disable bootstrap again by removing those env vars or setting `BootstrapAdmin__Enabled=false`.

## Database provider setup (MSSQL + PostgreSQL)

`WebApp` supports two EF Core database providers:

- `Postgres` (default; also accepts `PostgreSql`/`Npgsql`)
- `SqlServer`

Configuration keys:

- `DatabaseProvider`: `Postgres` (default) or `SqlServer`
- `ConnectionStrings:DefaultConnection` (SQL Server)
- `ConnectionStrings:PostgresConnection` (PostgreSQL)

Example PostgreSQL override (zsh/bash):

```bash
DatabaseProvider=Postgres \
ConnectionStrings__PostgresConnection="Host=localhost;Port=5432;Database=servmon;Username=postgres;Password=postgres" \
dotnet run --project WebApp/ServMonWeb.csproj
```

### EF Migrations

Existing Identity migrations were originally scaffolded for SQL Server.
With PostgreSQL as the default provider, generate/apply provider-specific PostgreSQL migrations before production use.

```bash
# Apply migrations (default Postgres provider)
dotnet ef database update --project WebApp/ServMonWeb.csproj

# Apply migrations (SQL Server)
DatabaseProvider=SqlServer dotnet ef database update --project WebApp/ServMonWeb.csproj
```

## CI/CD

The project includes a GitHub Actions workflow (`.github/workflows/ci.yml`) with a matrix build across:
- `windows-latest`
- `ubuntu-latest`
- `macos-latest`

Current trigger mode:
- Manual only via `workflow_dispatch` (auto `push` / `pull_request` triggers are commented out).

Steps: restore, build, test, publish artifacts.

## Migration status

Full migration details, gates, and rollback notes: [docs/DOTNET10_CROSS_PLATFORM_CHECKLIST.md](docs/DOTNET10_CROSS_PLATFORM_CHECKLIST.md).
