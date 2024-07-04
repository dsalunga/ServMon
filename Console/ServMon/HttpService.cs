using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ServMon
{
    class HttpService : CommonService
    {
        public override ServResponse Execute()
        {
            PreExecute();

            // Ignore invalid SSL certificates
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            var callSuccess = false;
            var text = string.Empty;
            var response = new ServResponse();
            try
            {
                var req = WebRequest.Create(Url);
                var res = req.GetResponse();
                var data = res.GetResponseStream();
                using (var sr = new StreamReader(data))
                {
                    text = sr.ReadToEnd();
                }
                res.Close();
                callSuccess = true;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.StackTrace = ex.ToString();
            }

            if (callSuccess)
            {
                if (text.Contains(Content))
                    response.Success = true;
                else
                    response.Message = string.Format("Expected response not found - {0}", Content);
            }

            PostExecute(response);
            return response;
        }
    }
}
