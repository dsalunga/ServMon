using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ServMon
{
    class SmsSettings
    {
        public bool Enabled { get; set; }
        public string Url { get; set; }
        public string To { get; set; }

        public string FromName { get; set; }
        public string FromNumber { get; set; }

        public string GetFrom(string to)
        {
            var t = Regex.Replace(to, @"[^\d]", "");

            if (t.Length == 11 && t.StartsWith("1"))
            {
                return FromNumber;
            }

            return FromName;
        }
    }
}
