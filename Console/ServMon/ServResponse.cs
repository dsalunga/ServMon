using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServMon
{
    class ServResponse
    {
        public ServResponse()
        {
            Success = false;
            Message = string.Empty;
            StackTrace = string.Empty;
        }

        public bool Success { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
    }
}
