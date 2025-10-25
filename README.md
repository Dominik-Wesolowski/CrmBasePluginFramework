# CrmBasePluginFramework

**A lightweight, structured, and production-ready framework for Dynamics 365 / Dataverse plugins**
Built for **clarity**, **consistency**, and **zero boilerplate tracing/configuration code**.

---

## 🚀 Overview

### 🔹 ExecutionObject

A unified runtime context that simplifies plugin execution.

It provides:

* `Context` (`IPluginExecutionContext`)
* `OrgService`, `OrgServiceAdmin`, `OrgServiceFactory`
* Entity accessors: `Target`, `TargetEntity`, `PreImage`, `PostImage`, and merged `FullTarget`
* Helpers:

  * `IsCreate`, `IsUpdate`, `IsDelete`
  * `IsChanged()`, `HasChangedAny()`
  * `TryGetSharedVariable<T>()`, `TryGetInputParameter<T>()`
* Built-in structured tracing via **`PluginTracingService`**, automatically configured on construction.

#### Automatic configuration

`ExecutionObject` automatically builds `PluginTracingService` from:

* **Environment Variable:** `new_PluginLoggingConfig` → contains `TrackingServiceConfig` JSON
* **Environment Variable (optional):** `debug_plugin_trace` → simple boolean to force full `Trace` level

The decision whether to log is made **inside the tracer**, based on the loaded configuration (`Config.ShouldLog(level)`).

---

### 🔹 PluginTracingService

A structured wrapper around `ITracingService` that adds:

* Log levels: `Trace`, `Information`, `Warning`, `Error`
* UTC ISO-8601 timestamps
* Pipeline metadata (`Message`, `Stage`, `Depth`, `Entity`, `CorrelationId`)
* `LogException()` helper
* Configurable via `TrackingServiceConfig`

**No manual gating** - all filtering is done via `ShouldLog(level)` inside the tracer.

Example output:

```
[2025-10-26T14:32:11.993Z][INFO][msg=Update;stage=40;depth=1;entity=account;corr=...]
Account updated successfully.
```

---

### 🔹 TrackingServiceConfig

Defines tracing behavior (stored as JSON inside EV `new_PluginLoggingConfig`):

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

| Property         | Type                      | Description                                                       |
| ---------------- | ------------------------- | ----------------------------------------------------------------- |
| **Enabled**      | `bool`                    | Turns logging on/off globally                                     |
| **MinimumLevel** | `string`                  | Minimal level to log (`Trace`, `Information`, `Warning`, `Error`) |
| **Levels**       | `Dictionary<string,bool>` | Optional per-level overrides                                      |

> 💡 If EV `debug_plugin_trace` = `true`, tracing is forced to `Trace` level:
>
> ```csharp
> cfg.Enabled = true;
> cfg.MinimumLevel = LogLevel.Trace;
> cfg.Levels[nameof(LogLevel.Trace)] = true;
> ```

---

## ⚙️ Configuration summary

| Setting                       | Type                 | Purpose                                                        | Example          |
| ----------------------------- | -------------------- | -------------------------------------------------------------- | ---------------- |
| **`new_PluginLoggingConfig`** | Environment Variable | Defines full structured logging config                         | JSON (see above) |
| **`debug_plugin_trace`**      | Environment Variable | Boolean flag (`true/false`) to temporarily force TRACE logging | `"true"`         |

---

## 🧱 BasePlugin

A minimal base class that:

* Creates `ExecutionObject`
* Delegates logic to your plugin
* Handles start/exception tracing

```csharp
public abstract class BasePlugin : IPlugin
{
    protected BasePlugin(string unsecure, string secure)
    {
        UnsecureConfig = unsecure;
        SecureConfig   = secure;
    }

    protected string UnsecureConfig { get; }
    protected string SecureConfig   { get; }

    public void Execute(IServiceProvider sp)
    {
        var exec = new ExecutionObject(sp, UnsecureConfig, SecureConfig);

        exec.TraceStart(GetType().FullName);
        try
        {
            Execute(exec);
        }
        catch (Exception ex)
        {
            exec.TraceException(ex);
            throw;
        }
    }

    protected abstract void Execute(ExecutionObject exec);
}
```

---

## 🧩 Example plugin

```csharp
public sealed class AccountPostUpdate : BasePlugin<Account>
{
    public AccountPostUpdate(string unsecure, string secure) : base(unsecure, secure) { }

    protected override void Execute(ExecutionObject<Account> exec)
    {
        exec.TracingService.LogInfo("Plugin started for account: {0}", exec.FullTargetEntity.Id);

        if (exec.IsChanged(Account.Fields.telephone1))
        {
            exec.TracingService.LogInfo("Phone changed to: {0}", exec.FullTargetEntity.telephone1);
        }
    }
}
```

Output when `debug_plugin_trace` = `true`:

```
[2025-10-26T14:31:08.551Z][VERB][msg=Update;stage=40;depth=1;entity=account;corr=...] 
Plugin started for account: 4fa1c0...
[2025-10-26T14:31:08.552Z][VERB] Phone changed to: +48 500 500 500
```

---

## 🧭 Migration guide

| Old version                            | Now                                                        |
| -------------------------------------- | ---------------------------------------------------------- |
| Manual `IsTracingEnabled` flag         | Removed - tracer decides                                   |
| Manual EV fetch / FetchXml             | Replaced by `ExecutionObject.GetSetting()`                 |
| Multiple config sources                | Only EVs (`new_PluginLoggingConfig`, `debug_plugin_trace`) |
| `BaseService` / external tracing setup | Removed - done automatically                               |
| `Trace()` calls                        | Use `TracingService.LogInfo()` etc.                        |

---

## 💡 Design principles

* **Single source of truth** for configuration → Environment Variables
* **No duplicated logic** between `ExecutionObject` and tracer
* **Simple, deterministic lifecycle:**
  `Plugin > ExecutionObject > PluginTracingService > Config`

---

## 🧠 TL;DR

✅ No flags, no FetchXml, no boilerplate
✅ Configurable per-environment via EV
✅ Structured tracing with contextual metadata
✅ Fully automatic setup in `ExecutionObject`

---

> “Write plugins that focus on business logic — not plumbing.”
> — *CrmBasePluginFramework*
