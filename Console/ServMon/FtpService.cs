using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WCMS.Common.Utilities;

namespace ServMon
{
    class FtpService : CommonService
    {
        public override ServResponse Execute()
        {
            PreExecute();

            var callSuccess = false;
            var text = string.Empty;
            var response = new ServResponse();
            var path = FtpHelper.UrlEncode(Url);
            try
            {
                var client = (FtpWebRequest)WebRequest.Create(path);
                client.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                client.Proxy = WebRequest.GetSystemWebProxy();
                client.Credentials = new NetworkCredential(Username, Password);
                using (var res = client.GetResponse() as FtpWebResponse)
                {
                    var reader = new StreamReader(res.GetResponseStream(), System.Text.Encoding.ASCII);
                    text = reader.ReadToEnd();
                    reader.Close();
                    res.Close();
                    callSuccess = true;
                }
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
