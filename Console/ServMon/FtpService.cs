using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentFTP;
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
            try
            {
                var uri = new Uri(Url);
                var host = uri.Host;
                var remotePath = Uri.UnescapeDataString(uri.AbsolutePath);

                using var client = new FtpClient(host);
                if (!string.IsNullOrEmpty(Username))
                    client.Credentials = new System.Net.NetworkCredential(Username, Password);
                client.Connect();

                var listing = client.GetListing(remotePath);
                var sb = new StringBuilder();
                foreach (var item in listing)
                {
                    sb.AppendLine(item.FullName);
                }
                text = sb.ToString();
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
