using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WCMS.Common;
using WCMS.Common.Utilities;

namespace ServMon
{
    class SmsSender
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public string To { get; set; }
        public string Message { get; set; }

        public void Send()
        {
            var settings = ServManager.Instance.SmsSettings;
            try
            {
                var recipients = To.TrimEnd(',').Split(',');
                foreach (var r in recipients)
                {
                    var recipient = Regex.Replace(r, @"[^\d]", "");
                    if (!string.IsNullOrEmpty(recipient))
                    {
                        var provider = new NamedValueProvider();
                        provider.Add("Number", recipient);
                        provider.Add("Message", Message);
                        provider.Add("From", settings.GetFrom(recipient));
                        var requestUrl = Substituter.Substitute(settings.Url, provider);

                        using var response = _httpClient.PostAsync(requestUrl, null).GetAwaiter().GetResult();
                        var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog(true, ex);
            }
        }
    }
}
