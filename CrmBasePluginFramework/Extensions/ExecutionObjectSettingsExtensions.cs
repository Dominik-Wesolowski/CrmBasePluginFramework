using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Security;

namespace CrmBasePluginFramework.Extensions;

public static class ExecutionObjectSettingsExtensions
{
    public static string GetSetting(
        this ExecutionObject exec,
        string key,
        bool includeEnvironmentVariables = true)
    {
        if (exec == null || string.IsNullOrWhiteSpace(key)) return null;

        if (key.Equals("Unsecure", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(exec.UnsecureConfig))
            return exec.UnsecureConfig;

        if (key.Equals("Secure", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(exec.SecureConfig))
            return exec.SecureConfig;

        if (!includeEnvironmentVariables || exec.OrgService == null) return null;
        var ev = TryReadEnvironmentVariable(exec.OrgService, key);
        return !string.IsNullOrWhiteSpace(ev) ? ev : null;
    }

    public static bool TryGetSetting(
        this ExecutionObject exec,
        string key,
        out string value,
        bool includeEnvironmentVariables = true)
    {
        value = GetSetting(exec, key, includeEnvironmentVariables);
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string TryReadEnvironmentVariable(IOrganizationService svc, string schemaName)
    {
        var fetch = $@"
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
</fetch>";

        var resp = svc.RetrieveMultiple(new FetchExpression(fetch));
        if (resp.Entities.Count == 0) return null;

        var row = resp.Entities[0];
        var val = row.GetAttributeValue<AliasedValue>("environmentvariablevalue.value")?.Value as string;
        return !string.IsNullOrWhiteSpace(val) ? val : row.GetAttributeValue<string>("defaultvalue");
    }
}