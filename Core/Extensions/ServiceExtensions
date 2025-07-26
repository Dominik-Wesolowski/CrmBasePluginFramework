using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Crm.Shared.Core.Extensions
{
    public static class ServiceExtensions
    {
        public static Guid CreateAndReturnId(this IOrganizationService service, Entity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return service.Create(entity);
        }

        public static bool UpdateIfChanged(this IOrganizationService service, Entity original, Entity modified)
        {
            if (original == null || modified == null) return false;
            if (original.Id != modified.Id || original.LogicalName != modified.LogicalName)
                throw new InvalidPluginExecutionException("Entity mismatch in UpdateIfChanged.");

            var delta = new Entity(original.LogicalName) { Id = original.Id };

            foreach (var kv in modified.Attributes)
            {
                if (!original.Attributes.ContainsKey(kv.Key) || !Equals(original[kv.Key], kv.Value))
                    delta[kv.Key] = kv.Value;
            }

            if (!delta.Attributes.Any()) return false;

            service.Update(delta);
            return true;
        }

        public static bool TryRetrieve(this IOrganizationService service, string logicalName, Guid id, ColumnSet columns, out Entity entity)
        {
            entity = null;
            try
            {
                entity = service.Retrieve(logicalName, id, columns);
                return entity != null;
            }
            catch
            {
                return false;
            }
        }

        public static T QueryFirst<T>(this IOrganizationService service, QueryExpression query)
            where T : Entity
        {
            return service.RetrieveMultiple(query).Entities.FirstOrDefault()?.ToEntity<T>();
        }

        public static List<T> QueryMany<T>(this IOrganizationService service, QueryExpression query)
            where T : Entity
        {
            return service.RetrieveMultiple(query).Entities.Select(e => e.ToEntity<T>()).ToList();
        }

        public static Guid Upsert(this IOrganizationService service, Entity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return entity.Id != Guid.Empty
                ? service.UpdateWithReturn(entity)
                : service.Create(entity);
        }

        public static Guid UpdateWithReturn(this IOrganizationService service, Entity entity)
        {
            service.Update(entity);
            return entity.Id;
        }

        public static bool Exists(this IOrganizationService service, string logicalName, Guid id)
        {
            try
            {
                var e = service.Retrieve(logicalName, id, new ColumnSet(false));
                return e != null;
            }
            catch
            {
                return false;
            }
        }

        public static TResponse Execute<TResponse>(this IOrganizationService service, OrganizationRequest request)
            where TResponse : OrganizationResponse
        {
            return (TResponse)service.Execute(request);
        }

        public static T GetByAttribute<T>(this IOrganizationService service, string logicalName, string attributeName, object value, ColumnSet columns)
            where T : Entity
        {
            var qe = new QueryExpression(logicalName)
            {
                ColumnSet = columns,
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression(attributeName, ConditionOperator.Equal, value) }
                },
                TopCount = 1
            };

            return service.QueryFirst<T>(qe);
        }
    }
}
