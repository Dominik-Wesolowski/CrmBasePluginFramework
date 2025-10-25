using CrmBasePluginFramework.Diagnostics;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Security;

namespace CrmBasePluginFramework.Extensions;

public static class ExecutionObjectSettingsExtensions
{
    public static string GetSetting(this ExecutionObject exec, string key, bool includeEnvironmentVariables = true)
    {
        if (exec == null || string.IsNullOrWhiteSpace(key)) return null;
        var ctx = exec.Context;

        if (ctx.TryGetSharedVariable<string>(key, out var sv) && !string.IsNullOrWhiteSpace(sv))
            return sv;

        if ((!string.IsNullOrWhiteSpace(exec.UnsecureConfig) &&
             key.Equals("Unsecure", StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(exec.SecureConfig) && key.Equals("Secure", StringComparison.OrdinalIgnoreCase)))
            return exec.SecureConfig;

        if (!includeEnvironmentVariables || exec.OrgService == null) return null;
        var ev = TryReadEnvironmentVariable(exec.OrgService, key);
        return !string.IsNullOrWhiteSpace(ev) ? ev : null;
    }

    public static TrackingServiceConfig GetLoggingConfig(this ExecutionObject exec, string configKey,
        TrackingServiceConfig @default)
    {
        var json = exec.GetSetting(configKey);
        return TrackingServiceConfig.FromJsonOrDefault(json, @default);
    }

    private static string TryReadEnvironmentVariable(IOrganizationService svc, string schemaName)
    {
        var fetch = $"""

                     <fetch top='1' no-lock='true'>
                       <entity name='environmentvariabledefinition'>
                         <attribute name='defaultvalue' />
                         <filter>
                           <condition attribute='schemaname' operator='eq' value='{SecurityElement.Escape(schemaName)}' />
                         </filter>
                         <link-entity name='environmentvariablevalue' from='environmentvariabledefinitionid' to='environmentvariabledefinitionid' link-type='outer'>
                           <attribute name='value' />
                           <order attribute='overriddencreatedon' descending='true' />
                         </link-entity>
                       </entity>
                     </fetch>
                     """;
        var resp = svc.RetrieveMultiple(new FetchExpression(fetch));
        if (resp.Entities.Count == 0) return null;

        var row = resp.Entities[0];
        var val = row.GetAttributeValue<AliasedValue>("environmentvariablevalue.value")?.Value as string;
        return !string.IsNullOrWhiteSpace(val) ? val : row.GetAttributeValue<string>("defaultvalue");
    }
}