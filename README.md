# ServMon

ServMon is a configurable service and endpoint monitor with a background agent and web dashboard for status and alerting.

## Project layout

### Modern stack (default)

| Path | Purpose | Runtime/Notes |
|-----|-----|-----|
| `ServMon.sln` | Default modern solution | Includes `Console/ServMon`, `WebApp`, `Shared/WCMS.Common`, `Shared/WCMS.Common.Tests` |
| `Console/ServMon` | Monitoring agent | `net10.0`, cross-platform |
| `WebApp` | ASP.NET Core MVC UI (`ServMonWeb.csproj`) | `net10.0`, branded `ServMon` |
| `Shared/WCMS.Common` | Shared utilities | `net10.0` |
| `Shared/WCMS.Common.Tests` | Unit tests for shared utilities | `net10.0` |

### Legacy stack (reference only)

| Path | Purpose | Runtime/Notes |
|-----|-----|-----|
| `legacy/ServMon.Legacy.sln` | Legacy-only solution | For legacy maintenance only |
| `legacy/ServMonWebCore` | Older ASP.NET Core MVC UI | `net8.0`, legacy, Windows-only support path |
| `legacy/ServMonWeb` | Legacy ASP.NET MVC 5 app | `.NET Framework 4.8.1`, Windows-only |
| `legacy/ServMonWeb/ServMonWeb.Tests` | Legacy MVC test project | `.NET Framework 4.8.1`, Windows-only |

> Legacy projects under `legacy/` are not part of the default `ServMon.sln` and are kept as read-only reference/history.

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

## First-run setup (fresh environment)

Use this once on a new machine/environment before normal operations.

`Console/ServMon/config.xml` is intentionally local-only (`.gitignore`).  
If it is missing, ServMon bootstraps it automatically from `Console/ServMon/config.sample.xml` on first access (console startup or web config/services access).

After bootstrap, review and replace placeholder values in `config.xml` (SMTP, recipients, endpoints, credentials) before production use.

```bash
# 1) Set database provider and connection string
export DatabaseProvider=Postgres
export ConnectionStrings__PostgresConnection="Host=localhost;Port=5432;Database=servmon;Username=postgres;Password=REPLACE_WITH_DB_PASSWORD"

# 2) Apply EF migrations
dotnet ef database update --project WebApp/ServMonWeb.csproj
```

For process-control/config-edit actions, bootstrap an Admin user once, then disable bootstrap immediately:

```bash
# 3) One-time admin bootstrap
export BootstrapAdmin__Enabled=true
export BootstrapAdmin__Email="admin@example.com"
export BootstrapAdmin__Password="REPLACE_WITH_STRONG_PASSWORD"
dotnet run --project WebApp/ServMonWeb.csproj

# 4) Disable bootstrap after first successful run
unset BootstrapAdmin__Enabled
unset BootstrapAdmin__Email
unset BootstrapAdmin__Password
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
ConnectionStrings__PostgresConnection="Host=localhost;Port=5432;Database=servmon;Username=postgres;Password=REPLACE_WITH_DB_PASSWORD" \
dotnet run --project WebApp/ServMonWeb.csproj
```

### EF Migrations

Existing Identity migrations were originally scaffolded for SQL Server.
With PostgreSQL as the default provider, generate/apply provider-specific PostgreSQL migrations before production use.

```bash
# Generate provider-specific migrations when schema changes (recommended separate folders)
DatabaseProvider=Postgres dotnet ef migrations add <MigrationName> --project WebApp/ServMonWeb.csproj --output-dir Data/Migrations/Postgres
DatabaseProvider=SqlServer dotnet ef migrations add <MigrationName> --project WebApp/ServMonWeb.csproj --output-dir Data/Migrations/SqlServer

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

Manual run options:
- GitHub UI: `Actions` -> `CI` -> `Run workflow`
- GitHub CLI:

```bash
gh workflow run ci.yml --ref master
```

## Migration status

Full migration details, gates, and rollback notes: [docs/DOTNET10_CROSS_PLATFORM_CHECKLIST.md](docs/DOTNET10_CROSS_PLATFORM_CHECKLIST.md).
