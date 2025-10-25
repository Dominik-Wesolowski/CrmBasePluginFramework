using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.PluginTelemetry;
using System;
using System.Text;

namespace CrmBasePluginFramework.Diagnostics;

public sealed class PluginTracingService(
    ITracingService inner,
    TrackingServiceConfig config,
    IPluginExecutionContext ctx)
    : ITracingService
{
    private readonly ITracingService _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public PluginTracingService(ITracingService inner, TrackingServiceConfig config)
        : this(inner, config, null)
    {
    }


    public TrackingServiceConfig Config { get; } = config ?? TrackingServiceConfig.DefaultOff;

    public void Trace(string format, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(format)) return;
        if (!Config.ShouldLog(LogLevel.Information)) return;

        Write(LogLevel.Information, format, args);
    }

    public void LogTrace(string message) => Log(LogLevel.Trace, message);
    public void LogTrace(string message, params object[] args) => Log(LogLevel.Trace, message, args);
    public void LogInfo(string message) => Log(LogLevel.Information, message);
    public void LogInfo(string message, params object[] args) => Log(LogLevel.Information, message, args);
    public void LogWarning(string message) => Log(LogLevel.Warning, message);
    public void LogWarning(string message, params object[] args) => Log(LogLevel.Warning, message, args);
    public void LogError(string message) => Log(LogLevel.Error, message);
    public void LogError(string message, params object[] args) => Log(LogLevel.Error, message, args);

    public void Log(LogLevel level, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (!Config.ShouldLog(level)) return;

        Write(level, message, null);
    }

    public void Log(LogLevel level, string message, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (!Config.ShouldLog(level)) return;

        Write(level, message, args);
    }

    public void LogException(Exception ex, string message = null)
    {
        if (ex == null) return;
        if (!Config.ShouldLog(LogLevel.Error)) return;

        if (!string.IsNullOrWhiteSpace(message))
            Write(LogLevel.Error, "{0} | Exception={1}", new object[] { message, ex.ToString() });
        else
            Write(LogLevel.Error, "Exception={0}", new object[] { ex.ToString() });
    }

    private void Write(LogLevel level, string template, object[] args)
    {
        var ts = DateTime.UtcNow.ToString("o"); // 2025-10-25T18:22:13.1234567Z
        var prefix = BuildPrefix(ts, level);
        var payload = args != null && args.Length > 0 ? SafeFormat(template, args) : template;

        _inner.Trace("{0} {1}", prefix, payload);
    }

    private string BuildPrefix(string ts, LogLevel level)
    {
        if (ctx == null)
            return $"[{ts}][{ShortTag(level)}]";

        var sb = new StringBuilder(128);
        sb.Append('[').Append(ts).Append(']');
        sb.Append('[').Append(ShortTag(level)).Append(']');
        sb.Append("[msg=").Append(ctx.MessageName).Append(';');
        sb.Append("stage=").Append(ctx.Stage).Append(';');
        sb.Append("depth=").Append(ctx.Depth).Append(';');
        sb.Append("entity=").Append(ctx.PrimaryEntityName).Append(';');
        sb.Append("corr=").Append(ctx.CorrelationId).Append(']');
        return sb.ToString();
    }

    private static string SafeFormat(string template, object[] args)
    {
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return $"{template} | (format_error args={args?.Length ?? 0})";
        }
    }

    private static string ShortTag(LogLevel level) => level switch
    {
        LogLevel.Trace => "VERB",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERR",
        var _ => level.ToString().ToUpperInvariant()
    };
}