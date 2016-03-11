using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Mail;
using WCMS.Common.Utilities;

namespace ServMon
{
    class MailSender
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }

        public void Send()
        {
            var settings = ServManager.Instance.MailSettings;
            using (var mail = new MailMessage())
            {
                try
                {
                    mail.From = new MailAddress(settings.From);
                    mail.To.Add(To.TrimEnd(','));
                    mail.Subject = Subject;
                    mail.Body = Message;
                    mail.IsBodyHtml = settings.IsBodyHtml;
                    // Can set to false, if you are sending pure text.

                    //mail.Attachments.Add(new Attachment("C:\\SomeFile.txt"));
                    //mail.Attachments.Add(new Attachment("C:\\SomeZip.zip"));

                    using (var smtp = new SmtpClient(settings.Host, settings.Port))
                    {
                        smtp.Credentials = new NetworkCredential(settings.From, settings.Password);
                        smtp.EnableSsl = settings.EnableSsl;
                        smtp.Send(mail);
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
}
