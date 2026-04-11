# .NET 10 Cross-Platform Migration Checklist

This checklist is designed to migrate ServMon to .NET 10 and make runtime/development workflows cross-platform (Windows, Linux, macOS) with controlled risk.

## Scope

- In scope:
  - `Console/ServMon`
  - `WebApp` (project `ServMonWeb`, primary web app)
  - `Shared/WCMS.Common`
  - `Shared/WCMS.Common.Tests`
- Out of scope (legacy):
  - `legacy/ServMonWeb` (`.NET Framework 4.8.1`)
  - `legacy/ServMonWeb/ServMonWeb.Tests` (`.NET Framework 4.8.1`)
  - `legacy/ServMonWebCore` (`net8.0`, legacy ASP.NET Core variant)

## Definitions

- Done criteria: objective condition that must pass before closing a task.
- Gate: stop/go checkpoint for the next phase.
- Rollback: pre-defined way to return to known-good behavior.

## Execution defaults (no pending clarifications)

- Branch strategy: execute on the current working branch (no dedicated migration branch).
- Modern web target naming: use `WebApp` folder and `ServMonWeb.csproj` project file.
- Modern/default solution name: `ServMon.sln` at repository root.
- Legacy-only solution name: `legacy/ServMon.Legacy.sln` (not used for default modern cross-platform builds).
- FTP modernization decision: replace `FtpWebRequest` with `FluentFTP` (or equivalent maintained client), no new `FtpWebRequest` usage.
- Evidence storage:
  - Build and validation outputs: `docs/migration/logs/`
  - Legacy local config examples: `docs/migration/legacy-config-examples.md`
- Default owner when unassigned: `Project maintainer`.

## Phase 0 - Baseline (Current Branch)

- [ ] Execute migration on the current working branch (no dedicated migration branch).
  - Done criteria: plan and tracker explicitly state no separate branch is required.
- [ ] Capture current build behavior and save logs under `docs/migration/logs/`.
  - Commands:
    - `dotnet --info`
    - `dotnet build Console/ServMon/ServMon.csproj -c Release`
    - `dotnet build WebApp/ServMonWeb.csproj -c Release`
    - `dotnet build ServMon.sln -c Release`
  - Done criteria: logs committed for baseline comparison.
- [ ] Create migration tracker issue (or board) with this checklist items.
  - Done criteria: each phase has an owner (`Project maintainer` by default) and target date.

Gate 0 (Go/No-Go):

- [ ] Decision recorded: projects under `legacy/` are non-blocking for cross-platform migration.

Rollback:

- Keep current release tag and deployment artifact before any code changes.

## Phase 1 - Isolate Legacy .NET Framework Projects

- [ ] Keep legacy projects under `legacy/` (already relocated) and out of modern build defaults.
  - Done criteria: `dotnet build ServMon.sln -c Release` succeeds on at least one non-Windows OS.
- [ ] Keep `ServMon.sln` as the single modern/default solution.
  - Include modern projects: `Console/ServMon`, `WebApp/ServMonWeb.csproj`, `Shared/WCMS.Common`, `Shared/WCMS.Common.Tests`.
  - Legacy projects remain in `legacy/` as Windows-only reference artifacts and must not block default cross-platform builds.
  - Keep `legacy/ServMon.Legacy.sln` for legacy-only work.
  - Done criteria: CI and local default commands use `ServMon.sln`.
- [ ] Update docs to mark legacy `.NET Framework` projects as Windows-only.
  - Done criteria: README clearly distinguishes modern vs legacy.

Gate 1:

- [ ] CI and local development can use `ServMon.sln` as default without legacy build blockers.

Rollback:

- Revert `ServMon.sln` modernization commit only (no runtime behavior changes yet).

## Phase 2 - Remove Platform-Specific Runtime Behavior

### 2.1 Console threading model

- [ ] Replace manual thread management with `Task` + `CancellationToken`.
  - Replace `Thread.Abort()` shutdown path.
  - Done criteria: graceful shutdown works without `SYSLIB0006`.
- [ ] Remove `SetApartmentState(ApartmentState.STA)` calls.
  - Done criteria: no `CA1416` warning for STA usage in `Console/ServMon`.

### 2.2 Network APIs modernization

- [ ] Replace `WebRequest` and `FtpWebRequest` usage with modern APIs.
  - HTTP/SMS: `HttpClient`.
  - FTP: replace with `FluentFTP` (or equivalent maintained client) and remove direct `FtpWebRequest` usage.
  - Done criteria: no `SYSLIB0014` warnings in console project.

### 2.3 Process startup behavior

- [ ] Make process executable path extension-independent.
  - Avoid hardcoded `.exe`.
  - Done criteria: same config works on Windows and non-Windows with environment-specific path value.

Gate 2:

- [ ] Console app starts, monitors, and stops cleanly on Windows and macOS/Linux.

Rollback:

- Feature flag or config switch to run old monitor bootstrap until new runner is stable.

## Phase 3 - Retarget to .NET 10

- [ ] Retarget `Console/ServMon` to `net10.0`.
- [ ] Retarget `WebApp/ServMonWeb.csproj` to `net10.0`.
- [ ] Add `global.json` to pin SDK major/minor used by CI.
- [ ] Upgrade package references to aligned versions compatible with .NET 10.

Done criteria:

- `dotnet restore` and `dotnet build` succeed for all in-scope projects.
- No new analyzer warnings introduced without explicit suppression rationale.

Gate 3:

- [ ] Code compiles on all target OS runners using pinned SDK.

Rollback:

- Revert target framework/package bump commit as a single unit if runtime regression is found.

## Phase 4 - Configuration and Paths Cross-Platform

- [ ] Remove absolute Windows paths from `appsettings*.json`.
  - Current examples use `C:\\...`; replace with relative paths or environment variables.
- [ ] Define canonical config keys for runtime paths:
  - `ServMon:ServicesJsonPath`
  - `ServMon:ConfigPath`
  - `ServMon:ExecutablePath`
- [ ] Support environment variable overrides in docs and examples.
- [ ] Normalize path handling with `Path.Combine` and `Path.DirectorySeparatorChar`.

Done criteria:

- App starts with no machine-specific absolute path edits.
- A fresh clone can run using documented `.env`/config values on each OS.

Gate 4:

- [ ] New developer setup works in < 15 minutes on macOS or Linux and on Windows.

Rollback:

- Keep previous local machine profile examples in `docs/migration/legacy-config-examples.md`.

## Phase 5 - Database Strategy for Cross-Platform

- [ ] Keep dual-provider support:
  - PostgreSQL (default, cross-platform)
  - SQL Server
- [ ] Remove LocalDB-only assumptions from appsettings and docs.
- [ ] Ensure EF migrations run on both providers (separate migration paths if needed).
- [ ] Add seed/migration command docs.

Done criteria:

- `dotnet ef database update` succeeds for SQL Server and PostgreSQL.
- App auth/data features work with both providers.

Gate 5:

- [ ] Dual-provider approach (MSSQL + PostgreSQL) is documented and verified.

Rollback:

- Maintain previous connection string profile for Windows dev until full provider migration is completed.

## Phase 6 - CI/CD Matrix and Quality Gates

- [ ] Add CI matrix for:
  - `windows-latest`
  - `ubuntu-latest`
  - `macos-latest`
- [ ] Pipeline steps:
  - restore
  - build
  - test (`dotnet test` for modern test projects; if none exist, run documented smoke checks)
  - publish artifact
- [ ] Add warnings policy:
  - Fail build on analyzers for in-scope projects after cleanup.
- [ ] Add basic smoke checks for web and console startup.

Done criteria:

- All matrix jobs are green on main branch.
- Build artifacts produced for all deployment targets.

Gate 6:

- [ ] Two consecutive green CI runs after migration changes are merged.

Rollback:

- Keep existing deployment workflow active until matrix pipeline is stable.

## Phase 7 - Cutover and Legacy Retirement

- [ ] Deploy .NET 10 modern stack to staging.
- [ ] Run monitoring/alert smoke tests in staging for at least 48 hours.
- [ ] Deploy to production with rollback window.
- [ ] Finalize legacy retirement for `.NET Framework` projects under `legacy/ServMonWeb`:
  - keep read-only for audit/history
  - remove from default build path

Done criteria:

- Production runs on .NET 10.
- Cross-platform builds are default in CI.
- Legacy projects no longer block normal engineering workflow.

Gate 7:

- [ ] Post-deploy review signed off by app owner and operations.

Rollback:

- Re-deploy last known-good artifact from pre-cutover tag.

## Task Board Template

Use this for tracking each item:

- [ ] Task:
- Owner:
- Target date:
- Dependencies:
- Validation command/evidence:
- Status note:

## Minimum Exit Criteria (Migration Complete)

- [ ] `Console/ServMon` and `WebApp/ServMonWeb.csproj` run on .NET 10.
- [ ] No hard dependency on Windows-only local development tooling.
- [ ] Cross-platform CI matrix is green.
- [ ] Legacy `.NET Framework` projects are isolated from default build pipeline.
- [ ] Runbook/docs updated for local setup, deployment, and rollback.
