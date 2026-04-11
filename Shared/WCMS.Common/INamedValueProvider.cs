using System;

namespace WCMS.Common
{
    public interface INamedValueProvider
    {
        string GetValue(string key);
        bool ContainsKey(string key);
        string this[string key] { get; set; }
        void Remove(string key);
    }
}
