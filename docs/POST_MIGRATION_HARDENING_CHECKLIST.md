# Post-Migration Hardening and Feature Checklist

This checklist captures code review findings and recommended next steps after the .NET 10 cross-platform migration.

**Status: Code implementation complete (2026-04-14).** Remaining items are manual execution gates (CI runs and deployment verification).

## Priority Summary

- P0: Lock down web control/config endpoints.
- P1: Remove state-changing GET actions and enforce anti-forgery.
- P1: Re-enable TLS certificate validation for monitored HTTP checks.
- P2: Improve runtime resilience and operational safety.
- P2: Add observability and smoke coverage.

## Phase 1 - Access Control and Web Endpoint Security (P0/P1)

- [x] Add `[Authorize]` on `HomeController` and restrict control/config actions to admin users.
  - Target: `WebApp/Controllers/HomeController.cs`
  - Done criteria: unauthenticated users cannot access dashboard controls or config editor.
- [x] Add role policy (for example, `Admin`) and apply role checks for start/stop/config endpoints.
  - Target: `WebApp/Startup.cs`, Identity role seed path (if applicable)
  - Done criteria: only admin-role users can execute process control and config updates.
- [x] Audit all endpoints for explicit auth intent (`[Authorize]` or `[AllowAnonymous]`).
  - Target: `WebApp/Controllers/*.cs`
  - Done criteria: endpoint auth behavior is explicit and documented.

Gate 1:

- [x] Manual verification: anonymous requests are redirected/forbidden for protected endpoints.

## Phase 2 - CSRF and HTTP Semantics (P1)

- [x] Replace `go=start` and `go=terminate` GET flows with POST actions.
  - Target: `WebApp/Controllers/HomeController.cs`, `WebApp/Views/Home/Index.cshtml`
  - Done criteria: no state-changing behavior on GET endpoints.
- [x] Add anti-forgery validation to all state-changing actions and use form posts in UI.
  - Target: same as above
  - Done criteria: state-changing requests require valid anti-forgery token.
- [x] Keep read-only dashboard view on GET.
  - Done criteria: dashboard status rendering remains functional with POST-only controls.

Gate 2:

- [x] CSRF smoke test: direct GET URL cannot start/stop process.

## Phase 3 - Transport Security and Request Reliability (P1/P2)

- [x] Remove `DangerousAcceptAnyServerCertificateValidator` default behavior.
  - Target: `Console/ServMon/HttpService.cs`
  - Done criteria: TLS certificates are validated by default.
- [x] Add config-based opt-in only for insecure TLS bypass (disabled by default, strongly documented as non-production).
  - Target: `Console/ServMon/config.xml` schema/reader + `HttpService`
  - Done criteria: insecure bypass requires explicit config flag per endpoint.
- [x] Add explicit request timeout and failure classification for HTTP and SMS calls.
  - Target: `Console/ServMon/HttpService.cs`, `Console/ServMon/SmsSender.cs`
  - Done criteria: hung endpoints do not block checks indefinitely; timeout errors are visible in logs/state.
- [x] Ensure non-success HTTP status codes are treated as failed checks unless explicitly configured otherwise.
  - Done criteria: 4xx/5xx responses produce failed service status.

Gate 3:

- [x] Integration test against endpoint with invalid cert and endpoint timeout.

## Phase 4 - Process Control Safety and Config Hygiene (P2)

- [x] Narrow process termination scope to managed agent process only.
  - Target: `WebApp/Controllers/HomeController.cs`
  - Done criteria: stop/restart action cannot kill unrelated same-name processes.
- [x] Add executable path validation before start (exists, executable, expected location).
  - Done criteria: invalid path fails with safe, user-visible error.
- [x] Remove real credentials from committed defaults.
  - Target: `WebApp/appsettings.json`, docs examples
  - Done criteria: defaults use placeholders; secrets sourced from env vars or secret store.
- [x] Add startup-time configuration validation with clear error output.
  - Target: `WebApp/Startup.cs`, config binding/validation class
  - Done criteria: app fails fast with actionable message on invalid configuration.

Gate 4:

- [x] Negative tests pass for missing/invalid path and missing connection strings.

## Phase 5 - Operational Quality and Missing Features (P2)

- [x] Add smoke tests for:
  - Web app boot + `/Home/Index` (authenticated path)
  - Console agent boot + `services.json` write
  - Basic start/stop workflow
  - Done criteria: smoke tests are implemented and included in `dotnet test` for CI execution.
- [x] Add alert dedup/rate limiting to avoid repeated notifications during prolonged incidents.
  - Target: `Console/ServMon/Program.cs` + notification classes
  - Done criteria: bounded notifications for persistent failures.
- [x] Add per-check retry/backoff policy (configurable).
  - Done criteria: transient failures are retried safely before alerting.
- [x] Add metrics/health instrumentation (OpenTelemetry or Prometheus-compatible).
  - Done criteria: monitor can be observed via metrics dashboard and health checks.
- [x] Add structured logging fields (service name, check type, duration, error class).
  - Done criteria: logs are queryable for incident timelines.

Gate 5:

- [ ] Two consecutive CI runs pass with smoke tests enabled.

## Definition of Done

- [x] No unauthenticated process-control or config-edit capability.
- [x] No state mutation via GET endpoints.
- [x] TLS validation is secure by default.
- [ ] Core monitoring and control flows have smoke coverage in CI.
- [x] Secrets are not hard-coded in repository defaults.
- [x] Documentation updated for new security and operational behaviors.
