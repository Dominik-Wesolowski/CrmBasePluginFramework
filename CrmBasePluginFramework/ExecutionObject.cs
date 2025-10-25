using CrmBasePluginFramework.Diagnostics;
using CrmBasePluginFramework.Extensions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.PluginTelemetry;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CrmBasePluginFramework;

public class ExecutionObject : IExecutionServiceBag
{
    private const string DebugFlagSchemaName = "debug_plugin_trace"; // EV: bool
    private const string LoggingConfigKey = "new_PluginLoggingConfig"; // EV: JSON (TrackingServiceConfig)

    private bool _isPreLoaded, _isPostLoaded, _isTargetLoaded, _isEntityLoaded;
    private Entity _preImage, _postImage, _targetEntity;
    private object _target;

    public ExecutionObject(IServiceProvider provider)
    {
        ServiceProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        Context = (IPluginExecutionContext)provider.GetService(typeof(IPluginExecutionContext));
        OrgServiceFactory = (IOrganizationServiceFactory)provider.GetService(typeof(IOrganizationServiceFactory));
        OrgService = OrgServiceFactory.CreateOrganizationService(Context.InitiatingUserId);
        OrgServiceAdmin = OrgServiceFactory.CreateOrganizationService(null);

        UnsecureConfig = null;
        SecureConfig = null;

        InitTracing(ServiceProvider);
    }

    public ExecutionObject(IServiceProvider provider, string unsecureConfig, string secureConfig) : this(provider)
    {
        // nadpisanie konfiguracji kroku i ponowna inicjalizacja trasingu
        UnsecureConfig = unsecureConfig;
        SecureConfig = secureConfig;
        InitTracing(ServiceProvider);
    }

    public ExecutionObject(ExecutionObject source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        ServiceProvider = source.ServiceProvider;
        Context = source.Context;
        OrgServiceFactory = source.OrgServiceFactory;
        OrgService = source.OrgService;
        OrgServiceAdmin = source.OrgServiceAdmin;
        TracingService = source.TracingService;

        UnsecureConfig = source.UnsecureConfig;
        SecureConfig = source.SecureConfig;

        _isPreLoaded = source._isPreLoaded;
        _isPostLoaded = source._isPostLoaded;
        _isTargetLoaded = source._isTargetLoaded;
        _isEntityLoaded = source._isEntityLoaded;

        _preImage = source._preImage;
        _postImage = source._postImage;
        _targetEntity = source._targetEntity;
        _target = source._target;
    }

    public IServiceProvider ServiceProvider { get; }
    public IPluginExecutionContext Context { get; }

    public string UnsecureConfig { get; private set; }
    public string SecureConfig { get; private set; }

    public bool IsCreate
    {
        get => Context.MessageName.Equals("Create", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsUpdate
    {
        get => Context.MessageName.Equals("Update", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsDelete
    {
        get => Context.MessageName.Equals("Delete", StringComparison.OrdinalIgnoreCase);
    }

    public object Target
    {
        get
        {
            if (_isTargetLoaded) return _target;
            Context.InputParameters.TryGetValue(nameof(Target), out _target);
            _isTargetLoaded = true;
            return _target;
        }
    }

    public Entity TargetEntity
    {
        get
        {
            if (_isEntityLoaded) return _targetEntity;
            _targetEntity = Target as Entity;
            _isEntityLoaded = true;
            return _targetEntity;
        }
    }

    public Entity PreImage
    {
        get => LoadImage("PreImage", ref _preImage, ref _isPreLoaded, Context.PreEntityImages);
    }

    public Entity PostImage
    {
        get => LoadImage("PostImage", ref _postImage, ref _isPostLoaded, Context.PostEntityImages);
    }

    public Entity FullTarget
    {
        get
        {
            var name = TargetEntity?.LogicalName ?? PreImage?.LogicalName ?? PostImage?.LogicalName;
            var merged = new Entity(name);
            if (PreImage != null) merged.Merge(PreImage);
            if (TargetEntity != null) merged.Merge(TargetEntity);
            if (PostImage != null) merged.Merge(PostImage);
            return merged;
        }
    }

    public IOrganizationServiceFactory OrgServiceFactory { get; }
    public IOrganizationService OrgService { get; }
    public IOrganizationService OrgServiceAdmin { get; }
    public ITracingService TracingService { get; private set; }

    public ExecutionObject WithStepConfig(string unsecureConfig, string secureConfig)
    {
        UnsecureConfig = unsecureConfig;
        SecureConfig = secureConfig;
        InitTracing(ServiceProvider);
        return this;
    }

    public TConfig GetConfig<TConfig>() where TConfig : class
    {
        if (string.IsNullOrWhiteSpace(UnsecureConfig)) return null;
        try
        {
            return JsonConvert.DeserializeObject<TConfig>(UnsecureConfig);
        }
        catch
        {
            return null;
        }
    }

    public void TraceStart(string pluginType)
    {
        TracingService.LogInfo($"[START] {pluginType}");
        TracingService.LogInfo($"Message: {Context.MessageName} | Stage: {Context.Stage} | Depth: {Context.Depth}");
        TracingService.LogInfo($"PrimaryEntity: {Context.PrimaryEntityName} | CorrelationId: {Context.CorrelationId}");
    }

    public void TraceException(Exception ex)
    {
        if (ex == null) return;
        TracingService.LogError($"[EXCEPTION] {ex.Message}\n{ex.StackTrace}");
    }

    public void SetSharedVariable<T>(string key, T value) => Context.SharedVariables[key] = value;
    public bool TryGetSharedVariable<T>(string key, out T value) => Context.TryGetSharedVariable(key, out value);

    public bool HasSharedVariable(string key, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        => Context.HasSharedVariable(key, comparison);

    public bool IsMessage(string name) => Context.MessageName.Equals(name, StringComparison.OrdinalIgnoreCase);
    public ExecutionObject<T> ToEntity<T>() where T : Entity => new ExecutionObject<T>(this);

    private void InitTracing(IServiceProvider provider)
    {
        var rawTrace = (ITracingService)provider.GetService(typeof(ITracingService)); // zawsze surowy tracer
        var cfg = BuildLoggerConfig();
        TracingService = new PluginTracingService(rawTrace, cfg, Context);
    }

    private TrackingServiceConfig BuildLoggerConfig()
    {
        var json = this.GetSetting(LoggingConfigKey);
        var cfg = TrackingServiceConfig.FromJsonOrDefault(json, TrackingServiceConfig.DefaultOn);

        var debug = this.GetSetting(DebugFlagSchemaName);
        if (string.IsNullOrWhiteSpace(debug) || !bool.TryParse(debug, out var isDebug) || !isDebug) return cfg;
        cfg.Enabled = true;
        cfg.MinimumLevel = LogLevel.Trace;
        cfg.Levels ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        cfg.Levels[nameof(LogLevel.Trace)] = true;

        return cfg;
    }

    private static Entity LoadImage(string name, ref Entity field, ref bool loaded, EntityImageCollection collection)
    {
        if (loaded) return field;
        if (collection != null && collection.Contains(name))
            field = collection[name];
        loaded = true;
        return field;
    }
}

public class ExecutionObject<T> : ExecutionObject where T : Entity
{
    public ExecutionObject(IServiceProvider provider) : base(provider)
    {
    }

    public ExecutionObject(ExecutionObject baseObj) : base(baseObj)
    {
    }

    public new T TargetEntity
    {
        get => base.TargetEntity?.ToEntity<T>();
    }

    public T FullTargetEntity
    {
        get => FullTarget?.ToEntity<T>();
    }

    public new T PreImage
    {
        get => base.PreImage?.ToEntity<T>();
    }

    public new T PostImage
    {
        get => base.PostImage?.ToEntity<T>();
    }

    public void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidPluginExecutionException(message);
    }

    public bool TryGetInputParameter<TParam>(string key, out TParam value)
    {
        if (Context.InputParameters.TryGetValue(key, out var raw) && raw is TParam typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }
}

public interface IExecutionServiceBag
{
    IOrganizationServiceFactory OrgServiceFactory { get; }
    IOrganizationService OrgService { get; }
    IOrganizationService OrgServiceAdmin { get; }
    ITracingService TracingService { get; }
}