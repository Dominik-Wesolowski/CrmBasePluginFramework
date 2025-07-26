using System;
using System.Globalization;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Crm.Shared.Core.Extensions
{
    public static class EntityExtensions
    {
        public static Entity Merge(this Entity target, Entity source)
        {
            if (target == null || source == null || target.LogicalName != source.LogicalName)
                return target;

            foreach (var kv in source.Attributes)
                target[kv.Key] = kv.Value;

            return target;
        }

        public static Entity MergeIfNotExist(this Entity target, Entity source)
        {
            if (target == null || source == null || target.LogicalName != source.LogicalName)
                return target;

            foreach (var kv in source.Attributes)
            {
                if (!target.Contains(kv.Key))
                    target[kv.Key] = kv.Value;
            }

            return target;
        }

        public static T GetAliasedValue<T>(this Entity entity, string attributeName)
        {
            if (!entity.Attributes.TryGetValue(attributeName, out var raw))
                return default;

            return raw is AliasedValue av ? (T)av.Value : (T)raw;
        }

        public static T GetAliasedValue<T>(this Entity entity, string alias, string logicalName)
        {
            return entity.GetAliasedValue<T>($"{alias}.{logicalName}");
        }

        public static Entity GetAliasedEntity(this Entity entity, string alias, string logicalName, string primaryId = null, bool removeId = true)
        {
            var idName = primaryId ?? logicalName + "id";
            var aliased = new Entity(logicalName);

            foreach (var key in entity.Attributes.Keys)
            {
                if (key.StartsWith(alias + "."))
                {
                    var cleanKey = key.Replace(alias + ".", "");
                    aliased[cleanKey] = entity.GetAttributeValue<AliasedValue>(key)?.Value;
                }
            }

            if (aliased.Attributes.ContainsKey(idName))
            {
                aliased.Id = aliased.GetAttributeValue<Guid>(idName);
                if (removeId)
                    aliased.Attributes.Remove(idName);
            }

            return aliased;
        }

        public static T GetService<T>(this IServiceProvider provider)
        {
            return (T)provider.GetService(typeof(T));
        }

        public static T Retrieve<T>(this IOrganizationService service, EntityReference reference, ColumnSet columns)
            where T : Entity
        {
            if (reference == null || reference.Id == Guid.Empty)
                return null;

            return service.Retrieve(reference.LogicalName, reference.Id, columns).ToEntity<T>();
        }

        public static T Retrieve<T>(this IOrganizationService service, string logicalName, Guid? id, ColumnSet columns)
            where T : Entity
        {
            if (!id.HasValue || id == Guid.Empty)
                return null;

            return service.Retrieve(logicalName, id.Value, columns).ToEntity<T>();
        }

        public static bool HasValue(this EntityReference reference)
        {
            return reference != null && reference.Id != Guid.Empty;
        }

        public static bool IsNullOrEmpty(this EntityReference reference)
        {
            return reference == null || reference.Id == Guid.Empty;
        }

        public static decimal GetMoneyValue(this Entity entity, string attributeName)
        {
            return entity.GetAttributeValue<Money>(attributeName)?.Value ?? 0m;
        }

        public static int? GetOptionSetValue(this Entity entity, string attributeName)
        {
            return entity.GetAttributeValue<OptionSetValue>(attributeName)?.Value;
        }

        public static bool HasOptionSetValue(this Entity entity, string attributeName, int value)
        {
            return entity.GetOptionSetValue(attributeName) == value;
        }

        public static TEnum ToEnum<TEnum>(this OptionSetValue value) where TEnum : struct
        {
            return Enum.IsDefined(typeof(TEnum), value?.Value ?? -1)
                ? (TEnum)Enum.ToObject(typeof(TEnum), value.Value)
                : default;
        }

        public static string ToDebugString(this Entity entity)
        {
            return string.Join(", ", entity.Attributes.Select(kv => $"{kv.Key} = {kv.Value}"));
        }
    }
}
