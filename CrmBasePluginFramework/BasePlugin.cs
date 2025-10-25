using CrmBasePluginFramework.Extensions;
using Microsoft.Xrm.Sdk;
using System;

namespace CrmBasePluginFramework;

public abstract class BasePlugin(string unsecureConfig, string secureConfig) : IPlugin
{
    protected string UnsecureConfig { get; } = unsecureConfig;
    protected string SecureConfig { get; } = secureConfig;

    public void Execute(IServiceProvider serviceProvider)
    {
        var exec = new ExecutionObject(serviceProvider, UnsecureConfig, SecureConfig);

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
        finally
        {
            exec.TracingService.LogInfo($"[END] {GetType().FullName}");
        }
    }

    protected abstract void Execute(ExecutionObject exec);
}

public abstract class BasePlugin<T>(string unsecureConfig, string secureConfig)
    : BasePlugin(unsecureConfig, secureConfig)
    where T : Entity
{
    protected override void Execute(ExecutionObject exec)
        => Execute(exec.ToEntity<T>());

    protected abstract void Execute(ExecutionObject<T> exec);
}