using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;

namespace Crm.Shared.Core
{
    public class ExecutionObject : IExecutionServiceBag
    {
        private const string TraceFlagSchemaName = "debug_plugin_trace";

        private Entity preImage, postImage, targetEntity;
        private object target;
        private bool isPreLoaded, isPostLoaded, isTargetLoaded, isEntityLoaded;

        public IServiceProvider ServiceProvider { get; }
        public IPluginExecutionContext Context { get; }
        public ITracingService TracingService { get; }
        public IOrganizationServiceFactory OrgServiceFactory { get; }
        public IOrganizationService OrgService { get; }
        public IOrganizationService OrgServiceAdmin { get; }
        public string UnsecureConfig { get; }

        public bool IsTracingEnabled { get; private set; }

        public ExecutionObject(IServiceProvider provider)
        {
            this.ServiceProvider = provider;
            this.Context = (IPluginExecutionContext)provider.GetService(typeof(IPluginExecutionContext));
            this.OrgServiceFactory = (IOrganizationServiceFactory)provider.GetService(typeof(IOrganizationServiceFactory));
            this.OrgService = OrgServiceFactory.CreateOrganizationService(Context.InitiatingUserId);
            this.OrgServiceAdmin = OrgServiceFactory.CreateOrganizationService(null);
            this.TracingService = (ITracingService)provider.GetService(typeof(ITracingService));
            this.UnsecureConfig = (provider.GetService(typeof(IPluginExecutionContext)) as IPluginExecutionContext)?.SharedVariables["UnsecureConfig"] as string;
            this.IsTracingEnabled = ResolveTracingFlag();
        }

        public TConfig GetConfig<TConfig>() where TConfig : class
        {
            if (string.IsNullOrEmpty(UnsecureConfig)) return null;
            try { return JsonConvert.DeserializeObject<TConfig>(UnsecureConfig); }
            catch { return null; }
        }

        public void Trace(string message)
        {
            if (IsTracingEnabled)
                TracingService?.Trace(message);
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
            if (!IsTracingEnabled) return;
            Trace($"[EXCEPTION] {ex.Message}\n{ex.StackTrace}");
        }

        public void SetSharedVariable<T>(string key, T value)
        {
            Context.SharedVariables[key] = value;
        }

        public bool TryGetSharedVariable<T>(string key, out T value)
        {
            if (Context.SharedVariables.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        public bool IsCreate => Context.MessageName.Equals("Create", StringComparison.OrdinalIgnoreCase);
        public bool IsUpdate => Context.MessageName.Equals("Update", StringComparison.OrdinalIgnoreCase);
        public bool IsDelete => Context.MessageName.Equals("Delete", StringComparison.OrdinalIgnoreCase);
        public bool IsMessage(string name) => Context.MessageName.Equals(name, StringComparison.OrdinalIgnoreCase);

        public object Target
        {
            get
            {
                if (!isTargetLoaded)
                {
                    Context.InputParameters.TryGetValue("Target", out target);
                    isTargetLoaded = true;
                }
                return target;
            }
        }

        public Entity TargetEntity
        {
            get
            {
                if (!isEntityLoaded)
                {
                    targetEntity = Target as Entity;
                    isEntityLoaded = true;
                }
                return targetEntity;
            }
        }

        public Entity PreImage => LoadImage("PreImage", ref preImage, ref isPreLoaded, Context.PreEntityImages);
        public Entity PostImage => LoadImage("PostImage", ref postImage, ref isPostLoaded, Context.PostEntityImages);

        public Entity FullTarget
        {
            get
            {
                var merged = new Entity(TargetEntity?.LogicalName ?? PreImage?.LogicalName ?? PostImage?.LogicalName);
                if (PreImage != null) merged.Merge(PreImage);
                if (TargetEntity != null) merged.Merge(TargetEntity);
                if (PostImage != null) merged.Merge(PostImage);
                return merged;
            }
        }

        private Entity LoadImage(string name, ref Entity field, ref bool loaded, EntityImageCollection collection)
        {
            if (!loaded)
            {
                if (collection.Contains(name))
                    field = collection[name];
                loaded = true;
            }
            return field;
        }

        public ExecutionObject<T> ToEntity<T>() where T : Entity => new ExecutionObject<T>(this);

        private bool ResolveTracingFlag()
        {
            if (TryGetSharedVariable("ForceTrace", out bool sharedFlag) && sharedFlag) return true;
            if (Context.InputParameters.TryGetValue("Debug", out var debugObj) && debugObj is bool debugFlag && debugFlag) return true;
            if (IsDebugTraceEnabledFromEnvironment()) return true;

            try
            {
                var config = JsonConvert.DeserializeObject<PluginConfig>(UnsecureConfig);
                return config?.DebugTrace == true;
            }
            catch { return false; }
        }

        private bool IsDebugTraceEnabledFromEnvironment()
        {
            try
            {
                var query = new QueryExpression("environmentvariabledefinition")
                {
                    ColumnSet = new ColumnSet("schemaname"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("schemaname", ConditionOperator.Equal, TraceFlagSchemaName)
                        }
                    },
                    LinkEntities =
                    {
                        new LinkEntity
                        {
                            LinkFromEntityName = "environmentvariabledefinition",
                            LinkFromAttributeName = "environmentvariabledefinitionid",
                            LinkToEntityName = "environmentvariablevalue",
                            LinkToAttributeName = "environmentvariabledefinitionid",
                            JoinOperator = JoinOperator.Inner,
                            Columns = new ColumnSet("value"),
                            EntityAlias = "val"
                        }
                    }
                };

                var result = OrgService.RetrieveMultiple(query).Entities.FirstOrDefault();
                var value = result?.GetAttributeValue<AliasedValue>("val.value")?.Value?.ToString();
                return bool.TryParse(value, out var parsed) && parsed;
            }
            catch (Exception ex)
            {
                TracingService?.Trace($"[TraceFlag] Error retrieving env variable '{TraceFlagSchemaName}': {ex.Message}");
                return false;
            }
        }

        private class PluginConfig
        {
            public bool DebugTrace { get; set; }
        }
    }

    public class ExecutionObject<T> : ExecutionObject where T : Entity
    {
        public ExecutionObject(IServiceProvider provider) : base(provider) { }
        public ExecutionObject(ExecutionObject baseObj) : base(baseObj.ServiceProvider) { }

        public new T TargetEntity => base.TargetEntity?.ToEntity<T>();
        public T FullTargetEntity => base.FullTarget?.ToEntity<T>();
        public new T PreImage => base.PreImage?.ToEntity<T>();
        public new T PostImage => base.PostImage?.ToEntity<T>();

        public void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidPluginExecutionException(message);
        }

        public bool IsChanged(string attributeName)
        {
            if (TargetEntity == null || PreImage == null)
                return false;

            return TargetEntity.Attributes.Contains(attributeName)
                   && PreImage.Attributes.Contains(attributeName)
                   && !Equals(TargetEntity[attributeName], PreImage[attributeName]);
        }

        public bool HasChangedAny(params string[] attributeNames)
        {
            return attributeNames.Any(attr => IsChanged(attr));
        }

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
}
