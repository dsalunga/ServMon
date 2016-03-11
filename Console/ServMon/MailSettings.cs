using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServMon
{
    class MailSettings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool EnableSsl { get; set; }
        public string From { get; set; }
        public string Password { get; set; }
        public string To { get; set; }
        public bool IsBodyHtml { get; set; }
    }
}
