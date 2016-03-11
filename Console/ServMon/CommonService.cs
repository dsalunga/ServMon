using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ServMon
{
    abstract class CommonService : IServiceType
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public ServTypes Type { get; set; }

        public bool Enabled { get; set; }
        public bool EnableSms { get; set; }
        public string Content { get; set; }

        public string Username { get; set; }
        public string Password { get; set; }

        public int Interval { get; set; }
        public string ToEmails { get; set; }
        public string ToNumbers { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }

        public abstract ServResponse Execute();

        protected void PreExecute()
        {

        }

        protected void PostExecute(ServResponse response)
        {
            Success = response.Success;
            Message = response.Message;
            StackTrace = response.StackTrace;
            LastUpdate = DateTime.Now;
        }
    }
}
