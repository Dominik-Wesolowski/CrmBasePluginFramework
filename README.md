# CrmBasePluginFramework

A lightweight, opinionated framework for building **Dynamics 365 / Dataverse plugins**.
Focused on **clarity, maintainability, and full control** over tracing, configuration, and plugin execution context - without unnecessary abstractions or "magic".

---

## ✳️ Overview

### 🔹 ExecutionObject

Central runtime context for plugin execution.
Provides immediate access to:

* `IPluginExecutionContext` (`Context`)
* `IOrganizationService`, `IOrganizationServiceAdmin`, and `IOrganizationServiceFactory`
* `ITracingService` (already wrapped with structured logging)
* Entity images (`PreImage`, `PostImage`, `Target`, and merged `FullTarget`)
* Helpers for:

  * `IsChanged` / `HasChangedAny`
  * `IsCreate`, `IsUpdate`, `IsDelete`
  * `TryGetSharedVariable<T>()`, `HasSharedVariable()`, etc.

**Responsibilities:**

* Determines whether tracing is enabled (via `ForceTrace`, `Debug` flag, environment variable, or step config JSON).
* Automatically replaces the default CRM tracer with `PluginTracingService`.
* Resolves configuration (SharedVariables → Step configs → Environment Variables).

---

### 🔹 PluginTracingService

A structured logger wrapping `ITracingService`.
Provides:

* Level-based logging (`Trace`, `Info`, `Warning`, `Error`)
* ISO 8601 UTC timestamps
* Pipeline context metadata (`Message`, `Stage`, `Depth`, `Entity`, `CorrelationId`)
* `LogException()` for consistent error logging

Configured automatically from `TrackingServiceConfig` JSON (resolved by `ExecutionObject`).

---

### 🔹 TrackingServiceConfig

JSON-based configuration object that controls logging behavior:

* `Enabled`: enable/disable all logging
* `MinimumLevel`: minimum level to log (`Trace`, `Information`, `Warning`, `Error`)
* `Levels`: optional overrides for specific levels (case-insensitive)

Example:

```json
{
  "Enabled": true,
  "MinimumLevel": "Information",
  "Levels": {
    "Trace": false,
    "Information": true,
    "Warning": true,
    "Error": true
  }
}
```

---

### 🔹 BasePlugin / BasePlugin<T>

The minimal base classes for plugins:

* Instantiates `ExecutionObject` (passing `unsecure/secure` config).
* Handles execution lifecycle (`START` / `EXCEPTION` / `END`) via `ExecutionObject.Trace*()` methods.
* Contains no logging or environment fetch logic - everything is handled by `ExecutionObject`.

---

## ⚙️ How configuration is resolved

### 1️⃣ Debug tracing flag

`ExecutionObject.ResolveTracingFlag()` checks these sources, in order:

1. `SharedVariables["ForceTrace"] == true`
2. `InputParameters["Debug"] == true`
3. Environment variable **`debug_plugin_trace`** (`"true"/"false"`)
4. Step **Unsecure** JSON → `{ "DebugTrace": true }`

If any of these is `true`, tracing is enabled.

---

### 2️⃣ Logger configuration (`TrackingServiceConfig`)

`ExecutionObject` automatically loads logger settings from
**`sha_PluginLoggingConfig`**, using this cascade:

1. `SharedVariables`
2. Step configs (Unsecure/Secure)
3. Dataverse Environment Variable

---

## 🧩 JSON configuration examples

### 📘 Full logger config (Environment Variable or Step)

**Schema name:** `sha_PluginLoggingConfig`
**Value:**

```json
{
  "Enabled": true,
  "MinimumLevel": "Information",
  "Levels": {
    "Trace": false,
    "Information": true,
    "Warning": true,
    "Error": true
  }
}
```

#### Field reference:

| Property       | Type                      | Description                                                        |
| -------------- | ------------------------- | ------------------------------------------------------------------ |
| `Enabled`      | `bool`                    | Global switch (disable all logging if `false`).                    |
| `MinimumLevel` | `string`                  | Minimal level to log (`Trace`, `Information`, `Warning`, `Error`). |
| `Levels`       | `Dictionary<string,bool>` | Optional overrides (case-insensitive keys).                        |

**Examples:**

* Log only warnings and errors:

  ```json
  { "Enabled": true, "MinimumLevel": "Warning" }
  ```
* Enable all logs, including Trace:

  ```json
  {
    "Enabled": true,
    "MinimumLevel": "Information",
    "Levels": { "Trace": true }
  }
  ```
* Disable logging entirely:

  ```json
  { "Enabled": false }
  ```

---

### 🧠 Debug trace flag (EV or SharedVariables)

Separate flag to **force tracing** regardless of config:
**Schema name:** `debug_plugin_trace`
**Value:** `"true"` or `"false"`

> Alternatively:
> `SharedVariables["ForceTrace"] = true` or `InputParameters["Debug"] = true`

---

## 🧱 Example plugin

```csharp
public sealed class AccountPostUpdate : BasePlugin<Account>
{
    public AccountPostUpdate(string unsecure, string secure) : base(unsecure, secure) { }

    protected override void Execute(ExecutionObject<Account> exec)
    {
        exec.LogContextSummary();

        if (exec.IsChanged(Account.Fields.telephone1))
        {
            exec.TracingService.LogInfo("Phone changed for account={0}", exec.FullTargetEntity.Name);
            // business logic here...
        }
    }
}
```

✅ What happens automatically:

* `ExecutionObject` builds context and detects tracing mode.
* Loads `TrackingServiceConfig` (Shared → Step → EV).
* Wraps `ITracingService` with `PluginTracingService`.
* You just call `exec.TracingService.LogInfo()` or use helpers like `IsChanged()`.

---

## 🧭 Migration guide

If you used the previous version of this framework:

| Old responsibility                 | Now handled by                              |
| ---------------------------------- | ------------------------------------------- |
| FetchXml for `debug_plugin_trace`  | `ExecutionObject.GetSetting()`              |
| Custom tracer setup                | automatic via `ExecutionObject` constructor |
| BasePlugin logging setup           | removed - handled by ExecutionObject        |
| Manual `ITracingService` injection | automatic                                   |
| Step config parsing                | `ExecutionObject.WithStepConfig()`          |

### Steps to migrate

1. **Remove** any FetchXml/QueryExpression code fetching EVs.
   → `ExecutionObject` does this internally.

2. **Stop** wrapping tracers manually in `BasePlugin`.
   → `ExecutionObject` now injects `PluginTracingService`.

3. Move your existing logger JSON to one of:

   * EV `sha_PluginLoggingConfig`, or
   * Step Unsecure/Secure field, or
   * `SharedVariables["sha_PluginLoggingConfig"]`.

4. Replace any `trace.Trace(...)` calls with:

   ```csharp
   exec.TracingService.LogInfo("message");
   exec.TracingService.LogError("message");
   exec.TracingService.LogWarning("message");
   ```

---

## 🔍 Summary

| Area                              | Before                              | Now                                  |
| --------------------------------- | ----------------------------------- | ------------------------------------ |
| Tracing setup                     | Manual in BasePlugin or BaseService | Automatic in `ExecutionObject`       |
| Debug flag (`debug_plugin_trace`) | Manual FetchXml                     | Resolved via `GetSetting()`          |
| Logging                           | Unstructured `Trace()`              | Structured `PluginTracingService`    |
| Config source                     | Unsecure only                       | Shared → Step → Environment Variable |
| BasePlugin role                   | Heavy orchestration                 | Lightweight delegate                 |

---

## 🧩 Philosophy

> **BasePlugin should not do magic.**
>
> All environment, tracing, and config resolution logic lives in `ExecutionObject`.
> Your plugins stay small, testable, and predictable.

---

## 🧠 TL;DR

✅ Structured, level-based logging

✅ Auto-configured tracing

✅ Simple JSON configuration

✅ Config source cascade (Shared → Step → EV)

✅ No BaseService dependency

✅ Early-bound friendly helpers

---

> 💬 *“A clean foundation for building maintainable Dynamics 365 plugins.”*
