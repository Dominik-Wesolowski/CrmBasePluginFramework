using Microsoft.Xrm.Sdk.PluginTelemetry;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CrmBasePluginFramework.Diagnostics;

public sealed class TrackingServiceConfig
{
    public static readonly TrackingServiceConfig DefaultOn = new TrackingServiceConfig { Enabled = true }.Normalize();
    public static readonly TrackingServiceConfig DefaultOff = new TrackingServiceConfig { Enabled = false }.Normalize();

    public bool Enabled { get; set; } = true;
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    [JsonProperty]
    public Dictionary<string, bool> Levels { get; set; } =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    public static TrackingServiceConfig FromJsonOrDefault(string json, TrackingServiceConfig @default = null)
    {
        var fallback = (@default ?? DefaultOn).Clone();
        if (string.IsNullOrWhiteSpace(json)) return fallback;

        try
        {
            var cfg = JsonConvert.DeserializeObject<TrackingServiceConfig>(json);
            return (cfg ?? fallback).Normalize();
        }
        catch
        {
            return fallback;
        }
    }

    public bool ShouldLog(LogLevel level)
    {
        if (!Enabled) return false;

        if (Levels == null || Levels.Count == 0)
            return level >= MinimumLevel;

        if (Levels.TryGetValue(level.ToString(), out var allowed))
            return allowed;

        return level >= MinimumLevel;
    }

    public string ToJson() => JsonConvert.SerializeObject(this);

    public TrackingServiceConfig Normalize()
    {
        if (Levels == null)
        {
            Levels = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            return this;
        }

        if (!ReferenceEquals(Levels.Comparer, StringComparer.OrdinalIgnoreCase))
            Levels = new Dictionary<string, bool>(Levels, StringComparer.OrdinalIgnoreCase);

        return this;
    }

    public TrackingServiceConfig Clone() =>
        new()
        {
            Enabled = Enabled,
            MinimumLevel = MinimumLevel,
            Levels = Levels != null
                ? new Dictionary<string, bool>(Levels, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        };
}