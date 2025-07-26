using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;

namespace Crm.Shared.Core
{
    public abstract class BasePlugin : IPlugin
    {
        protected BasePlugin(string unsecureConfig, string secureConfig)
        {
            this.UnsecureConfig = unsecureConfig;
            this.SecureConfig = secureConfig;
        }

        protected string UnsecureConfig { get; }
        protected string SecureConfig { get; }

        public void Execute(IServiceProvider serviceProvider)
        {
            var execution = CreateExecutionObject(serviceProvider);
            execution.TraceStart(this.GetType().FullName);

            try
            {
                Execute(execution);
            }
            catch (Exception ex)
            {
                execution.TraceException(ex);
                if (HandleGlobalException(execution, ex))
                    throw;
            }
        }

        protected virtual bool HandleGlobalException(ExecutionObject execution, Exception ex)
        {
            return true;
        }

        protected virtual ExecutionObject CreateExecutionObject(IServiceProvider provider)
            => new ExecutionObject(provider);

        public abstract void Execute(ExecutionObject execution);
    }

    public abstract class BasePlugin<T> : BasePlugin where T : Entity
    {
        protected BasePlugin(string unsecureConfig, string secureConfig) : base(unsecureConfig, secureConfig) { }

        public override void Execute(ExecutionObject execution)
        {
            var typed = execution.ToEntity<T>();
            Validate(typed);
            Execute(typed);
        }

        protected virtual void Validate(ExecutionObject<T> context) { }

        public abstract void Execute(ExecutionObject<T> context);
    }
}
