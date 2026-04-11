using System;
using System.Collections.Generic;
using System.Globalization;
using WCMS.Common.Utilities;

namespace WCMS.Common
{
    public class NamedValueProvider : INamedValueProvider
    {
        public Dictionary<string, string> Values { get; set; }

        public NamedValueProvider()
            : this(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        {
        }

        public NamedValueProvider(Dictionary<string, string> values)
        {
            Values = values ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public void Add(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            Values[key] = value;
        }

        public void Add(string key, object value)
        {
            Add(key, value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        public string Substitute(string format)
        {
            return Substituter.Substitute(format, this);
        }

        public string GetValue(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return Values.TryGetValue(key, out var value) ? value : null;
        }

        public bool ContainsKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && Values.ContainsKey(key);
        }

        public string this[string key]
        {
            get => GetValue(key);
            set
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    Values[key] = value;
                }
            }
        }

        public void Remove(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                Values.Remove(key);
            }
        }
    }
}
