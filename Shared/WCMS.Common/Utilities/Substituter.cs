using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using WCMS.Common;

namespace WCMS.Common.Utilities
{
    public static class Substituter
    {
        public delegate object ReplacementLookup(string name);

        public static string Substitute(string stringValue, IDictionary values)
        {
            return Substitute(stringValue, values, "$(", ")");
        }

        public static string Substitute(string stringValue, ReplacementLookup lookupCallback)
        {
            return Substitute(stringValue, lookupCallback, "$(", ")");
        }

        public static string Substitute(string stringValue, ReplacementLookup lookupCallback, string leftEnclose, string rightEnclose)
        {
            if (lookupCallback == null)
            {
                return stringValue;
            }

            return Substitute(
                stringValue,
                leftEnclose,
                rightEnclose,
                match => EvaluateMatch(match, sourceName => lookupCallback(sourceName)));
        }

        public static string Substitute(string stringValue, IDictionary values, string leftEnclose, string rightEnclose)
        {
            if (values == null)
            {
                return stringValue;
            }

            return Substitute(
                stringValue,
                leftEnclose,
                rightEnclose,
                match => EvaluateMatch(match, sourceName => values.Contains(sourceName) ? values[sourceName] : null));
        }

        public static string Substitute(string stringValue, string leftEnclose, string rightEnclose, MatchEvaluator matchEvaluator)
        {
            if (string.IsNullOrEmpty(stringValue) || matchEvaluator == null)
            {
                return stringValue;
            }

            return GetRegex(leftEnclose, rightEnclose).Replace(stringValue, matchEvaluator);
        }

        public static Regex GetRegex(string leftEnclose, string rightEnclose)
        {
            var left = Regex.Escape(string.IsNullOrEmpty(leftEnclose) ? "$(" : leftEnclose);
            var right = Regex.Escape(string.IsNullOrEmpty(rightEnclose) ? ")" : rightEnclose);
            return new Regex($"{left}(.*?){right}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }

        public static string Substitute(string stringValue, Dictionary<string, INamedValueProvider> context)
        {
            if (context == null)
            {
                return stringValue;
            }

            return Substitute(stringValue, sourceName => GetNamedValue(sourceName, context));
        }

        public static string Substitute(string stringValue, INamedValueProvider values)
        {
            if (values == null)
            {
                return stringValue;
            }

            return Substitute(stringValue, sourceName => values.GetValue(sourceName));
        }

        public static string Substitute(string templateString, string key, string value)
        {
            var values = new NamedValueProvider();
            values.Add(key, value);
            return Substitute(templateString, values);
        }

        public static string Substitute(string templateString, string defaultKeyValue)
        {
            var values = new NamedValueProvider();
            values.Add("value", defaultKeyValue);
            return Substitute(templateString, values);
        }

        public static string GetNamedValue(string sourceName, Dictionary<string, INamedValueProvider> context)
        {
            if (context == null || string.IsNullOrWhiteSpace(sourceName))
            {
                return null;
            }

            var separatorIndex = sourceName.IndexOf('.');
            if (separatorIndex > 0 && separatorIndex < sourceName.Length - 1)
            {
                var source = sourceName.Substring(0, separatorIndex);
                var key = sourceName.Substring(separatorIndex + 1);
                return context.TryGetValue(source, out var provider) ? provider?.GetValue(key) : null;
            }

            foreach (var provider in context.Values)
            {
                if (provider != null && provider.ContainsKey(sourceName))
                {
                    return provider.GetValue(sourceName);
                }
            }

            return null;
        }

        private static string EvaluateMatch(Match match, Func<string, object> lookup)
        {
            var token = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(token))
            {
                return match.Value;
            }

            var segments = token.Split('|');
            var sourceName = segments[0].Trim();
            var rawValue = lookup(sourceName);
            if (rawValue == null)
            {
                return match.Value;
            }

            var value = rawValue;
            string format = null;

            if (segments.Length == 2)
            {
                format = segments[1].Trim();
            }
            else if (segments.Length >= 3)
            {
                value = ConvertToType(rawValue, segments[1].Trim());
                format = segments[2].Trim();
            }

            if (!string.IsNullOrWhiteSpace(format))
            {
                if (value is IFormattable formattable)
                {
                    return formattable.ToString(format, CultureInfo.InvariantCulture);
                }

                if (DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    return parsedDate.ToString(format, CultureInfo.InvariantCulture);
                }
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static object ConvertToType(object value, string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName) || value == null)
            {
                return value;
            }

            var targetType = Type.GetType(typeName, throwOnError: false, ignoreCase: true);
            if (targetType == null)
            {
                return value;
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            var stringValue = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return value;
            }

            try
            {
                if (targetType == typeof(DateTime))
                {
                    return DateTime.Parse(stringValue, CultureInfo.InvariantCulture);
                }

                if (targetType.IsEnum)
                {
                    return Enum.Parse(targetType, stringValue, ignoreCase: true);
                }

                return Convert.ChangeType(stringValue, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return value;
            }
        }
    }
}
