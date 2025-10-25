using CrmBasePluginFramework.Diagnostics;
using CrmBasePluginFramework.Extensions;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace CrmBasePluginFramework;

public class ExecutionObject : IExecutionServiceBag
{
    private const string TraceFlagSchemaName = "debug_plugin_trace";

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

        var rawTrace = (ITracingService)provider.GetService(typeof(ITracingService));

        UnsecureConfig = null;
        SecureConfig = null;

        IsTracingEnabled = ResolveTracingFlag();

        var loggerCfgJson = this.GetSetting(TraceFlagSchemaName);
        var loggerCfg = TrackingServiceConfig.FromJsonOrDefault(loggerCfgJson, TrackingServiceConfig.DefaultOn);
        TracingService = new PluginTracingService(rawTrace, loggerCfg, Context);
    }

    public ExecutionObject(IServiceProvider provider, string unsecureConfig, string secureConfig)
        : this(provider)
    {
        UnsecureConfig = unsecureConfig;
        SecureConfig = secureConfig;

        var loggerCfgJson = this.GetSetting(TraceFlagSchemaName);
        var loggerCfg = TrackingServiceConfig.FromJsonOrDefault(loggerCfgJson, TrackingServiceConfig.DefaultOn);
        TracingService = new PluginTracingService((PluginTracingService)TracingService ?? TracingService,
            loggerCfg, Context);
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

        IsTracingEnabled = source.IsTracingEnabled;

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

    public bool IsTracingEnabled { get; }

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
            Context.InputParameters.TryGetValue("Target", out _target);
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

    public ITracingService TracingService { get; set; }
    public IOrganizationServiceFactory OrgServiceFactory { get; }
    public IOrganizationService OrgService { get; }
    public IOrganizationService OrgServiceAdmin { get; }

    public ExecutionObject WithStepConfig(string unsecureConfig, string secureConfig)
    {
        UnsecureConfig = unsecureConfig;
        SecureConfig = secureConfig;
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

    public void Trace(string message)
    {
        if (IsTracingEnabled) TracingService?.Trace(message);
    }

    public void TraceStart(string pluginType)
    {
        if (!IsTracingEnabled) return;
        Trace($"[START] {pluginType}");
        Trace($"Message: {Context.MessageName} | Stage: {Context.Stage} | Depth: {Context.Depth}");
        Trace($"PrimaryEntity: {Context.PrimaryEntityName} | CorrelationId: {Context.CorrelationId}");
    }

    public void TraceException(Exception ex)
    {
        if (!IsTracingEnabled || ex == null) return;
        Trace($"[EXCEPTION] {ex.Message}\n{ex.StackTrace}");
    }

    public void SetSharedVariable<T>(string key, T value) => Context.SharedVariables[key] = value;
    public bool TryGetSharedVariable<T>(string key, out T value) => Context.TryGetSharedVariable(key, out value);

    public bool HasSharedVariable(string key, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        => Context.HasSharedVariable(key, comparison);

    public bool IsMessage(string name) => Context.MessageName.Equals(name, StringComparison.OrdinalIgnoreCase);
    public ExecutionObject<T> ToEntity<T>() where T : Entity => new ExecutionObject<T>(this);

    private static Entity LoadImage(string name, ref Entity field, ref bool loaded, EntityImageCollection collection)
    {
        if (loaded) return field;
        if (collection != null && collection.Contains(name))
            field = collection[name];
        loaded = true;
        return field;
    }

    private bool ResolveTracingFlag()
    {
        if (Context.TryGetSharedVariable("ForceTrace", out bool force) && force) return true;

        if (Context.InputParameters.TryGetValue("Debug", out var debugObj) &&
            debugObj is bool debugFlag && debugFlag) return true;

        if (IsDebugTraceEnabledFromEnvironment()) return true;

        try
        {
            if (!string.IsNullOrWhiteSpace(UnsecureConfig))
            {
                var cfg = JsonConvert.DeserializeObject<PluginConfig>(UnsecureConfig);
                if (cfg?.DebugTrace == true) return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private bool IsDebugTraceEnabledFromEnvironment()
    {
        var value = this.GetSetting(TraceFlagSchemaName);
        if (string.IsNullOrWhiteSpace(value)) return false;
        return bool.TryParse(value, out var parsed) && parsed;
    }

    private class PluginConfig
    {
        public bool DebugTrace { get; set; }
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

    public bool IsChanged(string attributeName)
    {
        var tgt = base.TargetEntity;
        var pre = base.PreImage;

        if (tgt == null) return false;
        if (!tgt.Attributes.Contains(attributeName)) return false;

        var newVal = tgt[attributeName];
        if (pre == null || !pre.Attributes.Contains(attributeName)) return true;

        var oldVal = pre[attributeName];
        return !Equals(newVal, oldVal);
    }

    public bool HasChangedAny(params string[] attributeNames)
        => attributeNames != null && attributeNames.Any(IsChanged);

    public void LogContextSummary()
    {
        if (!IsTracingEnabled) return;

        Trace("[Context Summary]");
        Trace($"Message        : {Context.MessageName}");
        Trace($"Stage          : {Context.Stage} ({(PluginStage)Context.Stage})");
        Trace($"Depth          : {Context.Depth}");
        Trace($"Mode           : {(Context.Mode == 0 ? "Synchronous" : "Asynchronous")}");
        Trace($"Entity         : {Context.PrimaryEntityName} ({Context.PrimaryEntityId})");
        Trace($"User           : {Context.UserId} / Initiating: {Context.InitiatingUserId}");
        Trace($"CorrelationId  : {Context.CorrelationId}");
        Trace($"IsOffline      : {Context.IsExecutingOffline}");
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

public enum PluginStage
{
    PreValidation = 10,
    PreOperation = 20,
    MainOperation = 30,
    PostOperation = 40
}