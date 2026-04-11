using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ServMon
{
    class HttpService : CommonService
    {
        private static readonly HttpClient _httpClient;

        static HttpService()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            _httpClient = new HttpClient(handler);
        }

        public override ServResponse Execute()
        {
            PreExecute();

            var callSuccess = false;
            var text = string.Empty;
            var response = new ServResponse();
            try
            {
                using var httpResponse = _httpClient.GetAsync(Url).GetAwaiter().GetResult();
                text = httpResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
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
