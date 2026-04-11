# Legacy Config Examples (Pre-Migration)

This file stores legacy local-machine configuration samples used before cross-platform normalization.

## Purpose

- Preserve prior Windows-centric examples for rollback/debug reference.
- Avoid losing known-good local settings during migration.

## Legacy `appsettings.json` (Windows)

```json
{
  "appSettings": {
    "ServMon:ServicesJsonPath": "C:\\Workspace\\github.com\\ServMon\\Console\\ServMon\\bin\\Debug\\services.json",
    "ServMon:ConfigPath": "C:\\Workspace\\github.com\\ServMon\\Console\\ServMon\\bin\\Debug\\config.xml",
    "ServMon:ProcessName": "ServMon",
    "ServMon:ExecutablePath": "C:\\Workspace\\github.com\\ServMon\\Console\\ServMon\\bin\\Debug\\ServMon.exe",
    "ServMon:AgentAutoStart": "0",
    "ServMon:EnableRegister": "1",
    "ServMon:EnableEditConfig": "1"
  }
}
```

## Modern `appsettings.json` (Cross-Platform)

```json
{
  "appSettings": {
    "ServMon:ServicesJsonPath": "../Console/ServMon/bin/Debug/net10.0/services.json",
    "ServMon:ConfigPath": "../Console/ServMon/config.xml",
    "ServMon:ProcessName": "ServMon",
    "ServMon:ExecutablePath": "../Console/ServMon/bin/Debug/net10.0/ServMon",
    "ServMon:AgentAutoStart": "0",
    "ServMon:EnableRegister": "1",
    "ServMon:EnableEditConfig": "1"
  }
}
```

## Environment Variable Overrides

All `appSettings` keys can be overridden via environment variables using `__` as separator:

```bash
export appSettings__ServMon:ServicesJsonPath="/path/to/services.json"
export appSettings__ServMon:ConfigPath="/path/to/config.xml"
export appSettings__ServMon:ExecutablePath="/path/to/ServMon"
```
