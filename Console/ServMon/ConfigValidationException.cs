using System;
using System.Collections.Generic;
using System.Linq;

namespace ServMon
{
    class ConfigValidationException : Exception
    {
        public ConfigValidationException(IEnumerable<string> errors)
            : base(BuildMessage(errors))
        {
            Errors = (errors ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
        }

        public IReadOnlyList<string> Errors { get; }

        private static string BuildMessage(IEnumerable<string> errors)
        {
            var items = (errors ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (items.Count == 0)
            {
                return "Invalid config.xml.";
            }

            return "Invalid config.xml:\n - " + string.Join("\n - ", items);
        }
    }
}
