using Microsoft.Xrm.Sdk;
using System;
using System.Globalization;

namespace CrmBasePluginFramework.Extensions
{
    public static class PluginContextSharedVarsExtensions
    {
        public static bool HasSharedVariable(this IPluginExecutionContext ctx, string key,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase)
            => TryGetSharedVariable<object>(ctx, key, out var _, comparison);

        public static bool TryGetSharedVariable<T>(this IPluginExecutionContext ctx, string key, out T value,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase, Func<object, T> customConverter = null)
        {
            value = default;
            if (ctx == null || string.IsNullOrWhiteSpace(key)) return false;

            var current = ctx;
            while (current != null)
            {
                if (TryFromCollection(current.SharedVariables, key, out value, comparison, customConverter))
                    return true;

                current = current.ParentContext;
            }

            return false;
        }

        private static bool TryFromCollection<T>(ParameterCollection vars, string key, out T value,
            StringComparison comparison, Func<object, T> customConverter)
        {
            value = default;
            if (vars == null || vars.Count == 0) return false;

            if (vars.Contains(key) && TryConvert(vars[key], out value, customConverter))
                return true;

            foreach (var k in vars.Keys)
            {
                if (k is string s && s.Equals(key, comparison))
                    return TryConvert(vars[k], out value, customConverter);
            }

            return false;
        }

        private static bool TryConvert<T>(object obj, out T value, Func<object, T> customConverter)
        {
            if (obj is T direct)
            {
                value = direct;
                return true;
            }

            if (customConverter != null)
                try
                {
                    value = customConverter(obj);
                    return true;
                }
                catch
                {
                    // ignored
                }

            if (obj == null)
            {
                value = default;
                return false;
            }

            var target = typeof(T);

            try
            {
                if (target == typeof(int))
                    switch (obj)
                    {
                        case OptionSetValue osv:
                            value = (T)(object)osv.Value;
                            return true;
                        case string si when int.TryParse(si, NumberStyles.Integer, CultureInfo.InvariantCulture,
                            out var iv):
                            value = (T)(object)iv;
                            return true;
                    }

                if (target == typeof(Guid))
                    switch (obj)
                    {
                        case Guid g:
                            value = (T)(object)g;
                            return true;
                        case EntityReference er:
                            value = (T)(object)er.Id;
                            return true;
                        case string sg when Guid.TryParse(sg, out var gv):
                            value = (T)(object)gv;
                            return true;
                    }

                if (target.IsEnum)
                {
                    if (obj is string es)
                    {
                        var parseMethod = typeof(Enum)
                            .GetMethod(nameof(Enum.TryParse),
                                new[] { typeof(string), typeof(bool), target.MakeByRefType() });

                        var parameters = new object[] { es, true, null };
                        var success = (bool)parseMethod.Invoke(null, parameters);
                        if (success && parameters[2] is T parsedEnum)
                        {
                            value = parsedEnum;
                            return true;
                        }
                    }

                    if (IsNumeric(obj))
                    {
                        var numericValue = Convert.ToInt64(obj, CultureInfo.InvariantCulture);
                        value = (T)Enum.ToObject(target, numericValue);
                        return true;
                    }
                }

                if (target == typeof(string))
                {
                    if (obj is OptionSetValue osv2)
                    {
                        value = (T)(object)osv2.Value.ToString(CultureInfo.InvariantCulture);
                        return true;
                    }

                    if (obj is EntityReference er2)
                    {
                        var s = !string.IsNullOrWhiteSpace(er2.Name) ? er2.Name : er2.Id.ToString();
                        value = (T)(object)s;
                        return true;
                    }

                    if (obj is Guid g2)
                    {
                        value = (T)(object)g2.ToString();
                        return true;
                    }
                }

                value = (T)Convert.ChangeType(obj, target, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        private static bool IsNumeric(object o)
        {
            switch (Type.GetTypeCode(o.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
                default:
                    return false;
            }
        }
    }
}