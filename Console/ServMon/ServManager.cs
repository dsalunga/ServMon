using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using WCMS.Common.Utilities;

namespace ServMon
{
    class ServManager
    {
        public ServManager()
        {
            Items = new Dictionary<string, IServiceType>();
            Interval = -1;
            MailSettings = new MailSettings();
            SmsSettings = new SmsSettings();
        }

        public void ReadConfig()
        {
            var servType = typeof(ServTypes);
            var xdoc = new XmlDocument();
            xdoc.Load("config.xml");
            var settingsNode = xdoc.SelectSingleNode("//Services/Settings");

            Items = new Dictionary<string, IServiceType>();
            Interval = DataHelper.GetInt32(XmlUtil.GetValue(settingsNode, "Interval"), 60 * 60);

            var mailNode = settingsNode.SelectSingleNode("Mail");
            MailSettings.EnableSsl = DataHelper.GetBool(XmlUtil.GetValue(mailNode, "EnableSsl"), true);
            MailSettings.Port = DataHelper.GetInt32(XmlUtil.GetValue(mailNode, "Port"), 587);
            MailSettings.IsBodyHtml = DataHelper.GetBool(XmlUtil.GetValue(mailNode, "IsBodyHtml"), true);
            MailSettings.Host = XmlUtil.GetValue(mailNode, "Host");
            MailSettings.From = XmlUtil.GetValue(mailNode, "From");
            MailSettings.To = XmlUtil.GetValue(mailNode, "To");
            MailSettings.Password = XmlUtil.GetValue(mailNode, "Password");

            var smsNode = settingsNode.SelectSingleNode("Sms");
            SmsSettings.Enabled = DataHelper.GetBool(XmlUtil.GetValue(smsNode, "Enabled"), false);
            SmsSettings.Url = XmlUtil.GetValue(smsNode, "Url");
            SmsSettings.To = XmlUtil.GetValue(smsNode, "To");
            SmsSettings.FromName = XmlUtil.GetValue(smsNode, "FromName");
            SmsSettings.FromNumber = Regex.Replace(XmlUtil.GetValue(smsNode, "FromNumber"), @"[^\d]", "");

            var nodes = xdoc.SelectNodes("//Services/Service");
            foreach (XmlNode node in nodes)
            {
                IServiceType service = null;
                var type = (ServTypes)Enum.Parse(servType, XmlUtil.GetValue(node, "Type"));
                switch (type)
                {
                    case ServTypes.HTTP:
                        service = new HttpService();
                        break;

                    case ServTypes.FTP:
                        service = new FtpService();
                        break;
                }

                if (service != null)
                {
                    service.Name = XmlUtil.GetValue(node, "Name");
                    service.Url = XmlUtil.GetValue(node, "Url");
                    service.Enabled = DataHelper.GetBool(XmlUtil.GetValue(node, "Enabled"), true);
                    service.Content = XmlUtil.GetValue(node, "Content");
                    service.Username = XmlUtil.GetValue(node, "Username");
                    service.Password = XmlUtil.GetValue(node, "Password");
                    service.Interval = DataHelper.GetInt32(XmlUtil.GetValue(node, "Interval"), 0);
                    service.ToEmails = XmlUtil.GetValue(node, "ToEmails");
                    service.ToNumbers = XmlUtil.GetValue(node, "ToNumbers");
                    service.EnableSms = DataHelper.GetBool(XmlUtil.GetValue(node, "EnableSms"), true);
                    Items.Add(service.Name, service);
                }
            }
        }

        public Dictionary<string, IServiceType> Items { get; set; }
        public int Interval { get; set; }
        public MailSettings MailSettings { get; set; }
        public SmsSettings SmsSettings { get; set; }

        private static ServManager _instance = null;
        public static ServManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ServManager();

                return _instance;
            }
        }

        public static ServResponse Execute(IServiceType item)
        {
            return item.Execute();
        }
    }
}
