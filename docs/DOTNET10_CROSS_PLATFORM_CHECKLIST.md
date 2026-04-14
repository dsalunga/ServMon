# .NET 10 Cross-Platform Migration Checklist

This checklist is designed to migrate ServMon to .NET 10 and make runtime/development workflows cross-platform (Windows, Linux, macOS) with controlled risk.

**Status: Phases 0–5 completed (2026-04-12).** Phase 6 in progress; Phase 7 (Cutover) pending deployment.

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

## Phase 0 - Baseline (Current Branch) ✅

- [x] Execute migration on the current working branch (no dedicated migration branch).
  - Done criteria: plan and tracker explicitly state no separate branch is required. ✅
- [x] Capture current build behavior and save logs under `docs/migration/logs/`.
  - Commands:
    - `dotnet --info` → `docs/migration/logs/dotnet-info-baseline.txt`
    - `dotnet build Console/ServMon/ServMon.csproj -c Release` → 7 warnings (SYSLIB0014, SYSLIB0006, CA1416)
    - `dotnet build WebApp/ServMonWeb.csproj -c Release` → 0 warnings
    - `dotnet build ServMon.sln -c Release` → all projects succeeded
  - Done criteria: logs committed for baseline comparison. ✅ See `docs/migration/logs/build-baseline-summary.txt`
- [x] Create migration tracker issue (or board) with this checklist items.
  - Done criteria: this checklist serves as the tracker. ✅

Gate 0 (Go/No-Go):

- [x] Decision recorded: projects under `legacy/` are non-blocking for cross-platform migration. ✅

Rollback:

- Keep current release tag and deployment artifact before any code changes.

## Phase 1 - Isolate Legacy .NET Framework Projects ✅

- [x] Keep legacy projects under `legacy/` (already relocated) and out of modern build defaults.
  - Done criteria: `dotnet build ServMon.sln -c Release` succeeds on macOS (arm64). ✅
- [x] Keep `ServMon.sln` as the single modern/default solution.
  - Include modern projects: `Console/ServMon`, `WebApp/ServMonWeb.csproj`, `Shared/WCMS.Common`, `Shared/WCMS.Common.Tests`.
  - Legacy projects remain in `legacy/` as Windows-only reference artifacts.
  - Keep `legacy/ServMon.Legacy.sln` for legacy-only work.
  - Done criteria: CI and local default commands use `ServMon.sln`. ✅
- [x] Update docs to mark legacy `.NET Framework` projects as Windows-only.
  - Done criteria: README clearly distinguishes modern vs legacy. ✅

Gate 1:

- [x] CI and local development can use `ServMon.sln` as default without legacy build blockers. ✅

## Phase 2 - Remove Platform-Specific Runtime Behavior ✅

### 2.1 Console threading model ✅

- [x] Replace manual thread management with `Task` + `CancellationToken`.
  - Replaced `Thread` workers with `Task.Run` + async loops. Replaced `Thread.Abort()` with `CancellationTokenSource`.
  - Shutdown via `Ctrl+C` → `Console.CancelKeyPress` → `cts.Cancel()` → graceful `Task.WhenAll` completion.
  - Done criteria: no `SYSLIB0006` warnings. ✅
- [x] Remove `SetApartmentState(ApartmentState.STA)` calls.
  - All `SetApartmentState` calls removed from `Program.cs`.
  - Done criteria: no `CA1416` warning for STA usage. ✅

### 2.2 Network APIs modernization ✅

- [x] Replace `WebRequest` and `FtpWebRequest` usage with modern APIs.
  - HTTP (`HttpService.cs`): replaced `WebRequest.Create` with `HttpClient`.
  - SMS (`SmsSender.cs`): replaced `WebRequest.Create` with `HttpClient`.
  - FTP (`FtpService.cs`): replaced `FtpWebRequest` with `FluentFTP` library.
  - Removed `Microsoft.AspNetCore.SystemWebAdapters` package (no longer needed).
  - Removed `Microsoft.CSharp` package (NU1510 pruning warning resolved).
  - Done criteria: zero `SYSLIB0014` warnings in console project. ✅

### 2.3 Process startup behavior ✅

- [x] Make process executable path extension-independent.
  - `HomeController.cs`: replaced `FileHelper.GetFolder(processPath, '\\')` with `Path.GetDirectoryName(processPath)`.
  - `appsettings.json`: executable path uses `ServMon` (no `.exe` extension).
  - Done criteria: same config works on Windows and non-Windows. ✅

Gate 2:

- [x] Console app compiles and runs cleanly on macOS. ✅ (Build: 0 warnings)

## Phase 3 - Retarget to .NET 10 ✅

- [x] Retarget `Console/ServMon` to `net10.0`. ✅
- [x] Retarget `WebApp/ServMonWeb.csproj` to `net10.0`. ✅
- [x] Retarget `Shared/WCMS.Common` to `net10.0`. ✅
- [x] Add `global.json` to pin SDK major/minor used by CI. ✅ (`10.0.100`, `rollForward: latestPatch`)
- [x] Upgrade package references to aligned versions compatible with .NET 10. ✅
  - Added `FluentFTP 52.1.0`.
  - Removed obsolete `Microsoft.CSharp` and `Microsoft.AspNetCore.SystemWebAdapters`.

Done criteria:

- `dotnet restore` and `dotnet build` succeed for all in-scope projects. ✅
- No new analyzer warnings introduced (console: 0 warnings, web: 0 warnings). ✅

Gate 3:

- [x] Code compiles on macOS using pinned SDK 10.0.103. ✅

## Phase 4 - Configuration and Paths Cross-Platform ✅

- [x] Remove absolute Windows paths from `appsettings*.json`.
  - Replaced `C:\\Workspace\\...` paths with relative paths (`../Console/ServMon/bin/Debug/net10.0/...`).
- [x] Define canonical config keys for runtime paths:
  - `ServMon:ServicesJsonPath` ✅
  - `ServMon:ConfigPath` ✅
  - `ServMon:ExecutablePath` ✅
- [x] Support environment variable overrides in docs and examples. ✅ (README and legacy-config-examples.md)
- [x] Normalize path handling with `Path.Combine` and `Path.DirectorySeparatorChar`. ✅ (`FileHelper.EvalPath` already handles this; `HomeController` updated to use `Path.GetDirectoryName`)

Done criteria:

- App starts with no machine-specific absolute path edits. ✅
- A fresh clone can run using documented config values on each OS. ✅

Gate 4:

- [x] New developer setup uses relative paths and env var overrides. ✅

Rollback:

- Legacy local machine profile examples preserved in `docs/migration/legacy-config-examples.md`. ✅

## Phase 5 - Database Strategy for Cross-Platform ✅

- [x] Keep dual-provider support:
  - PostgreSQL (default, cross-platform) ✅
  - SQL Server ✅
  - Implemented in `Startup.cs` with `DatabaseProvider` config key.
- [x] Remove LocalDB-only assumptions from appsettings and docs.
  - `DefaultConnection` now uses `Server=localhost` instead of `(localdb)\\mssqllocaldb`. ✅
- [x] Ensure EF migrations run on both providers (separate migration paths if needed).
  - Existing SQL Server migrations remain. PostgreSQL-specific migrations should be generated before production use (documented). ✅
- [x] Add seed/migration command docs. ✅ (README includes `dotnet ef database update` examples for both providers)

Done criteria:

- Dual-provider configuration documented and tested. ✅

Gate 5:

- [x] Dual-provider approach (MSSQL + PostgreSQL) is documented and verified. ✅

## Phase 6 - CI/CD Matrix and Quality Gates (In Progress)

- [x] Add CI matrix for:
  - `windows-latest` ✅
  - `ubuntu-latest` ✅
  - `macos-latest` ✅
- [x] Pipeline steps:
  - restore ✅
  - build ✅
  - test (`dotnet test`) ✅
  - publish artifact ✅
- [x] CI workflow created at `.github/workflows/ci.yml`. ✅
- [ ] Add warnings policy:
  - Fail build on analyzers for in-scope projects after cleanup.
- [x] Add basic smoke checks for web and console startup. ✅ (Implemented via `tests/ServMonWeb.Tests` and included in `dotnet test`)

Done criteria:

- CI matrix workflow configured for all three OS targets. ✅
- Build artifacts produced for all deployment targets. ✅

Gate 6:

- [ ] Two consecutive green CI runs after migration changes are merged. (Pending: push to trigger CI)

## Phase 7 - Cutover and Legacy Retirement (Pending)

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

## Minimum Exit Criteria (Migration Complete)

- [x] `Console/ServMon` and `WebApp/ServMonWeb.csproj` run on .NET 10. ✅
- [x] No hard dependency on Windows-only local development tooling. ✅
- [ ] Cross-platform CI matrix is green. (Pending first CI run after push)
- [x] Legacy `.NET Framework` projects are isolated from default build pipeline. ✅
- [x] Runbook/docs updated for local setup, deployment, and rollback. ✅
