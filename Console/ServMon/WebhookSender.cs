using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using WCMS.Common.Utilities;

namespace ServMon
{
    class WebhookSender
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        public string WebhookUrl { get; set; }
        public string Message { get; set; }
        public string Title { get; set; }

        public void Send()
        {
            if (string.IsNullOrWhiteSpace(WebhookUrl) || string.IsNullOrWhiteSpace(Message))
            {
                return;
            }

            try
            {
                var payload = new
                {
                    text = string.IsNullOrWhiteSpace(Title) ? Message : $"{Title}\n{Message}"
                };

                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                using var response = _httpClient.PostAsync(WebhookUrl, content).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    throw new HttpRequestException($"Webhook returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Response: {body}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog(true, ex);
            }
        }
    }
}
