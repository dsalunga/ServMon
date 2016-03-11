using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WCMS.Common;
using WCMS.Common.Utilities;

namespace ServMon
{
    class SmsSender
    {
        public string To { get; set; }
        public string Message { get; set; }

        public void Send()
        {
            var settings = ServManager.Instance.SmsSettings;
            try
            {
                // Prepare request

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

                        var text = string.Empty;
                        var req = WebRequest.Create(requestUrl);
                        req.Method = "POST";
                        req.ContentLength = 0;
                        //req.Expect = "application/json";
                        var res = req.GetResponse();
                        var data = res.GetResponseStream();
                        using (var sr = new StreamReader(data))
                        {
                            text = sr.ReadToEnd();
                        }
                        res.Close();

                        // check response ???
                    }
                }
            }
            //catch (SmtpFailedRecipientsException) { }
            catch (Exception ex)
            {
                LogHelper.WriteLog(true, ex);
            }
        }
    }
}
