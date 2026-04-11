using System;
using System.Globalization;

namespace WCMS.Common.Utilities
{
    public static class DataUtil
    {
        public static int GetInt32(object o)
        {
            return GetInt32(o, 0, false);
        }

        public static int GetInt32(object o, int defaultValue, bool nativeValue = false)
        {
            if (o == null || o == DBNull.Value)
            {
                return defaultValue;
            }

            if (o is int i)
            {
                return i;
            }

            if (o is bool b)
            {
                return b ? 1 : 0;
            }

            if (nativeValue && o is IConvertible)
            {
                try
                {
                    return Convert.ToInt32(o, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return defaultValue;
                }
            }

            var s = Convert.ToString(o, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(s))
            {
                return defaultValue;
            }

            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
            {
                return parsedInt;
            }

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDouble))
            {
                return Convert.ToInt32(parsedDouble);
            }

            if (bool.TryParse(s, out var parsedBool))
            {
                return parsedBool ? 1 : 0;
            }

            return defaultValue;
        }

        public static int GetInt32(string o)
        {
            return GetInt32(o, 0);
        }

        public static int GetInt32(string o, int defaultValue)
        {
            return GetInt32((object)o, defaultValue, false);
        }

        public static bool GetBool(string o, bool valueIfNull = false)
        {
            if (string.IsNullOrWhiteSpace(o))
            {
                return valueIfNull;
            }

            var normalized = o.Trim();
            if (bool.TryParse(normalized, out var parsedBool))
            {
                return parsedBool;
            }

            switch (normalized.ToLowerInvariant())
            {
                case "1":
                case "y":
                case "yes":
                case "on":
                    return true;
                case "0":
                case "n":
                case "no":
                case "off":
                    return false;
                default:
                    return valueIfNull;
            }
        }

        public static bool GetBool(object o, bool defaultValue = false)
        {
            if (o == null || o == DBNull.Value)
            {
                return defaultValue;
            }

            if (o is bool b)
            {
                return b;
            }

            if (o is byte bt)
            {
                return bt != 0;
            }

            if (o is short sh)
            {
                return sh != 0;
            }

            if (o is int i)
            {
                return i != 0;
            }

            if (o is long l)
            {
                return l != 0;
            }

            return GetBool(Convert.ToString(o, CultureInfo.InvariantCulture), defaultValue);
        }
    }
}
