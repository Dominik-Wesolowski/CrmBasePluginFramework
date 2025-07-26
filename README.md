# Crm.BasePluginFramework

**Stop repeating yourself in every Dynamics 365 plugin.**

`Crm.PluginFramework` is a lightweight but powerful base framework for building Microsoft Dataverse / Dynamics 365 plugins and service components.  
It gives you a **solid execution model**, **clean extensibility**, and **battle-tested utility functions** - all designed for real-world CRM projects.

---

## Why use it?

Every experienced CRM developer has built their own BasePlugin and helper extensions.  
This repo gives you a production-ready, polished version of that foundation:
  - 🧠 Typed plugin execution model with full access to services & images
  - 🧰 Entity & service extension methods that prevent bugs and reduce code noise
  - 💬 Trace-friendly, testable, readable plugin code
  - 🔁 Smart diff updates and safe CRUD shortcuts
  - 🧼 No dependency injection, no runtime magic - works with the out-of-the-box plugin pipeline

If you've ever written this:

```csharp
var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
var target = (Entity)context.InputParameters["Target"];
var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
var service = serviceFactory.CreateOrganizationService(context.UserId);
```
...then this framework is for you.

## What's included?

  - BasePlugin - Abstract class with built-in tracing, config parsing, and error handling
  - ExecutionObject - One-liner access to Context, Target, Images, Services, SharedVariables, etc.
  - ExecutionObject<T> - Strongly-typed version with FullTargetEntity, PreImage, PostImage


## Entity & service extensions

  - Entity merging - Merge(), MergeIfNotExist(), ToDebugString()
  - OptionSets - ToEnum<T>(), HasOptionSetValue(...)
  - Aliased values - GetAliasedValue<T>(), GetAliasedEntity()
  - Money / References - GetMoneyValue(...), HasValue()
  - Service utilities - TryRetrieve(...), Exists(...), QueryFirst<T>(), UpdateIfChanged()
  - CRUD wrappers - Upsert(), CreateAndReturnId(), UpdateWithReturn()
  - Typed Execute - Execute<TResponse>(OrganizationRequest)

```csharp
public class ContactPlugin : BasePlugin<Contact>
{
    public ContactPlugin(string unsecure, string secure) : base(unsecure, secure) {}

    public override void Execute(ExecutionObject<Contact> context)
    {
        var contact = context.FullTargetEntity;
        context.Trace("Contact: {0}", contact.FullName);

        if (!context.OrgService.Exists("contact", contact.Id))
            throw new InvalidPluginExecutionException("Missing contact.");

        var existing = context.OrgService.Retrieve<Contact>(contact.ToEntityReference(), new ColumnSet("firstname"));
        var updated = existing.Merge(contact);

        context.OrgService.UpdateIfChanged(existing, updated);
    }
}
```
No more context.InputParameters["Target"], no more if (context.InputParameters.Contains("Target")).
## 🚀 Designed for
  - Dynamics 365 CE / On-Prem
  - Dataverse (Power Platform)
  - Plugin Registration Tool / Azure Plugins
  - .NET Framework 4.6.2+ or .NET 7/8

## Why this repo exists

This framework was born from years of CRM development pain - repeated plugin scaffolding, inconsistent context handling, and too much serviceProvider.GetService(...).
It’s designed for clean, scalable, and reliable plugin development.
Use it in your next project and never write boilerplate again.

## Installation / Suggested Project Structure

You can include this framework in your plugin solution using a shared code project, e.g.:
```
/Your.Solution/
│
├── Plugin.Contact/
│ └── (uses BasePlugin, ExecutionObject, etc.)
│
├── Plugin.Account/
│ └── (uses shared services, entity helpers, etc.)
│
├── Shared.PluginFramework/
│ └── Core/
│ ├── BasePlugin.cs
│ ├── ExecutionObject.cs
│ ├── Extensions/
│ │ ├── EntityExtensions.cs
│ │ └── ServiceExtensions.cs
```

This allows reuse across all your plugins and background jobs.  
You can name the shared project:

- `Shared.PluginFramework`  
- `Crm.Core`
- `Dataverse.Base`
- `Xrm.Shared`

...whatever fits your naming convention.

Make sure to reference this shared project from each plugin-specific project.

## 📄 License

MIT - free to use and modify.
