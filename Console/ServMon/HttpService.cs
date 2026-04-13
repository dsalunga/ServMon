using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServMon
{
    class HttpService : CommonService
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        private static readonly HttpClient _secureClient;
        private static readonly HttpClient _insecureClient;

        static HttpService()
        {
            _secureClient = new HttpClient { Timeout = DefaultTimeout };

            var insecureHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            _insecureClient = new HttpClient(insecureHandler) { Timeout = DefaultTimeout };
        }

        public override ServResponse Execute()
        {
            PreExecute();

            var callSuccess = false;
            var text = string.Empty;
            var response = new ServResponse();
            var client = AllowInsecureTls ? _insecureClient : _secureClient;
            try
            {
                using var httpResponse = client.GetAsync(Url).GetAwaiter().GetResult();
                if (!httpResponse.IsSuccessStatusCode)
                {
                    response.Message = $"HTTP {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}";
                    PostExecute(response);
                    return response;
                }
                text = httpResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                callSuccess = true;
            }
            catch (TaskCanceledException)
            {
                response.Message = $"Request timed out after {DefaultTimeout.TotalSeconds}s";
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
