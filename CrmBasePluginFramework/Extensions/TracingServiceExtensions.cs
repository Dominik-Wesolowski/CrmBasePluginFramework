using CrmBasePluginFramework.Diagnostics;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.PluginTelemetry;
using System;
using System.Text;

namespace CrmBasePluginFramework.Extensions;

public static class TracingServiceExtensions
{
    public static void Log(this ITracingService trace, LogLevel level, string message)
        => LogCore(trace, level, message, null);

    public static void Log(this ITracingService trace, LogLevel level, string message, params object[] args)
        => LogCore(trace, level, message, args);

    public static void LogTrace(this ITracingService trace, string message)
        => LogCore(trace, LogLevel.Trace, message, null);

    public static void LogTrace(this ITracingService trace, string message, params object[] args)
        => LogCore(trace, LogLevel.Trace, message, args);

    public static void LogInfo(this ITracingService trace, string message)
        => LogCore(trace, LogLevel.Information, message, null);

    public static void LogInfo(this ITracingService trace, string message, params object[] args)
        => LogCore(trace, LogLevel.Information, message, args);

    public static void LogWarning(this ITracingService trace, string message)
        => LogCore(trace, LogLevel.Warning, message, null);

    public static void LogWarning(this ITracingService trace, string message, params object[] args)
        => LogCore(trace, LogLevel.Warning, message, args);

    public static void LogError(this ITracingService trace, string message)
        => LogCore(trace, LogLevel.Error, message, null);

    public static void LogError(this ITracingService trace, string message, params object[] args)
        => LogCore(trace, LogLevel.Error, message, args);

    public static void LogException(this ITracingService trace, Exception ex, string message = null,
        params object[] args)
    {
        if (trace == null || ex == null) return;

        if (trace is PluginTracingService pts)
        {
            pts.Log(LogLevel.Error,
                !string.IsNullOrWhiteSpace(message)
                    ? AppendException(SafeFormat(message, args), ex)
                    : AppendException("Unhandled exception", ex));
            return;
        }

        var payload = !string.IsNullOrWhiteSpace(message)
            ? AppendException(SafeFormat(message, args), ex)
            : AppendException("Unhandled exception", ex);

        trace.Trace(Prefix(LogLevel.Error) + " " + payload);
    }

    private static void LogCore(ITracingService trace, LogLevel level, string message, object[] args)
    {
        if (trace == null || string.IsNullOrWhiteSpace(message)) return;

        if (trace is PluginTracingService pts)
        {
            if (args == null || args.Length == 0)
                pts.Log(level, message);
            else
                pts.Log(level, message, args);
            return;
        }

        var payload = args == null || args.Length == 0 ? message : SafeFormat(message, args);
        trace.Trace(Prefix(level) + " " + payload);
    }

    private static string SafeFormat(string template, object[] args)
    {
        try
        {
            return string.Format(template, args ?? []);
        }
        catch
        {
            var argCount = args?.Length ?? 0;
            return $"{template} | (format_error args={argCount})";
        }
    }

    private static string AppendException(string message, Exception ex)
    {
        var sb = new StringBuilder(message?.Length + 32 ?? 64);
        if (!string.IsNullOrWhiteSpace(message)) sb.Append(message).Append(" | ");
        sb.Append("Exception=").Append(ex);
        return sb.ToString();
    }

    private static string Prefix(LogLevel level) =>
        "[" + ShortTag(level) + "]";

    private static string ShortTag(LogLevel level) => level switch
    {
        LogLevel.Trace => "VERB",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERR",
        var _ => level.ToString().ToUpperInvariant()
    };
}