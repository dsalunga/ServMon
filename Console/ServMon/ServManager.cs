using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using WCMS.Common.Utilities;

namespace ServMon
{
    class ServManager
    {
        private static readonly Regex EnvParenPattern = new Regex(@"\$\(\s*env:(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex EnvBracePattern = new Regex(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public ServManager()
        {
            Items = new Dictionary<string, IServiceType>(StringComparer.OrdinalIgnoreCase);
            Interval = -1;
            MailSettings = new MailSettings();
            SmsSettings = new SmsSettings();
            AlertSettings = new AlertSettings();
        }

        public void ReadConfig()
        {
            var configPath = EnsureConfigFile("config.xml");
            var xdoc = new XmlDocument();
            xdoc.Load(configPath);

            var errors = new List<string>();
            var rootNode = xdoc.SelectSingleNode("/Services");
            if (rootNode == null)
            {
                throw new ConfigValidationException(new[] { "Missing root node '/Services'." });
            }

            var settingsNode = rootNode.SelectSingleNode("Settings");
            if (settingsNode == null)
            {
                throw new ConfigValidationException(new[] { "Missing required node '/Services/Settings'." });
            }

            var interval = ParsePositiveInt(
                ResolveEnvironmentPlaceholders(XmlUtil.GetValue(settingsNode, "Interval"), "/Services/Settings/Interval", errors),
                "/Services/Settings/Interval",
                defaultValue: 60 * 60,
                errors: errors,
                required: true);

            var mailSettings = ParseMailSettings(settingsNode.SelectSingleNode("Mail"), errors);
            var smsSettings = ParseSmsSettings(settingsNode.SelectSingleNode("Sms"), errors);
            var alertSettings = ParseAlertSettings(settingsNode.SelectSingleNode("Alerts"), errors);
            var serviceItems = ParseServiceItems(rootNode, alertSettings, errors);

            if (errors.Count > 0)
            {
                throw new ConfigValidationException(errors);
            }

            Interval = interval;
            MailSettings = mailSettings;
            SmsSettings = smsSettings;
            AlertSettings = alertSettings;
            Items = serviceItems;
        }

        private MailSettings ParseMailSettings(XmlNode mailNode, List<string> errors)
        {
            if (mailNode == null)
            {
                errors.Add("Missing required node '/Services/Settings/Mail'.");
                return new MailSettings();
            }

            var settings = new MailSettings
            {
                EnableSsl = ParseBool(
                    ResolveEnvironmentPlaceholders(XmlUtil.GetValue(mailNode, "EnableSsl"), "/Services/Settings/Mail/EnableSsl", errors),
                    defaultValue: true,
                    path: "/Services/Settings/Mail/EnableSsl",
                    errors: errors),
                IsBodyHtml = ParseBool(
                    ResolveEnvironmentPlaceholders(XmlUtil.GetValue(mailNode, "IsBodyHtml"), "/Services/Settings/Mail/IsBodyHtml", errors),
                    defaultValue: true,
                    path: "/Services/Settings/Mail/IsBodyHtml",
                    errors: errors),
                Host = GetRequiredValue(mailNode, "Host", "/Services/Settings/Mail/Host", errors),
                From = GetRequiredValue(mailNode, "From", "/Services/Settings/Mail/From", errors),
                To = GetOptionalValue(mailNode, "To", "/Services/Settings/Mail/To", errors),
                Password = GetOptionalValue(mailNode, "Password", "/Services/Settings/Mail/Password", errors)
            };

            settings.Port = ParsePositiveInt(
                GetOptionalValue(mailNode, "Port", "/Services/Settings/Mail/Port", errors),
                "/Services/Settings/Mail/Port",
                defaultValue: 587,
                errors: errors,
                required: false);

            return settings;
        }

        private SmsSettings ParseSmsSettings(XmlNode smsNode, List<string> errors)
        {
            if (smsNode == null)
            {
                return new SmsSettings
                {
                    Enabled = false
                };
            }

            var settings = new SmsSettings
            {
                Enabled = ParseBool(
                    ResolveEnvironmentPlaceholders(XmlUtil.GetValue(smsNode, "Enabled"), "/Services/Settings/Sms/Enabled", errors),
                    defaultValue: false,
                    path: "/Services/Settings/Sms/Enabled",
                    errors: errors),
                Url = GetOptionalValue(smsNode, "Url", "/Services/Settings/Sms/Url", errors),
                To = GetOptionalValue(smsNode, "To", "/Services/Settings/Sms/To", errors),
                FromName = GetOptionalValue(smsNode, "FromName", "/Services/Settings/Sms/FromName", errors),
                FromNumber = Regex.Replace(GetOptionalValue(smsNode, "FromNumber", "/Services/Settings/Sms/FromNumber", errors), @"[^\d]", "")
            };

            if (settings.Enabled && string.IsNullOrWhiteSpace(settings.Url))
            {
                errors.Add("SMS is enabled but '/Services/Settings/Sms/Url' is empty.");
            }

            return settings;
        }

        private AlertSettings ParseAlertSettings(XmlNode alertsNode, List<string> errors)
        {
            var settings = new AlertSettings();
            if (alertsNode == null)
            {
                return settings;
            }

            settings.DefaultAlertThresholdFailures = ParsePositiveInt(
                GetOptionalValue(alertsNode, "DefaultAlertThresholdFailures", "/Services/Settings/Alerts/DefaultAlertThresholdFailures", errors),
                "/Services/Settings/Alerts/DefaultAlertThresholdFailures",
                settings.DefaultAlertThresholdFailures,
                errors,
                required: false);

            settings.DefaultAlertCooldownSeconds = ParseNonNegativeInt(
                GetOptionalValue(alertsNode, "DefaultAlertCooldownSeconds", "/Services/Settings/Alerts/DefaultAlertCooldownSeconds", errors),
                "/Services/Settings/Alerts/DefaultAlertCooldownSeconds",
                settings.DefaultAlertCooldownSeconds,
                errors,
                required: false);

            settings.DefaultEscalationThresholdFailures = ParsePositiveInt(
                GetOptionalValue(alertsNode, "DefaultEscalationThresholdFailures", "/Services/Settings/Alerts/DefaultEscalationThresholdFailures", errors),
                "/Services/Settings/Alerts/DefaultEscalationThresholdFailures",
                settings.DefaultEscalationThresholdFailures,
                errors,
                required: false);

            settings.DefaultEscalationCooldownSeconds = ParseNonNegativeInt(
                GetOptionalValue(alertsNode, "DefaultEscalationCooldownSeconds", "/Services/Settings/Alerts/DefaultEscalationCooldownSeconds", errors),
                "/Services/Settings/Alerts/DefaultEscalationCooldownSeconds",
                settings.DefaultEscalationCooldownSeconds,
                errors,
                required: false);

            settings.WebhookEnabled = ParseBool(
                ResolveEnvironmentPlaceholders(XmlUtil.GetValue(alertsNode, "WebhookEnabled"), "/Services/Settings/Alerts/WebhookEnabled", errors),
                defaultValue: false,
                path: "/Services/Settings/Alerts/WebhookEnabled",
                errors: errors);

            settings.WebhookUrl = GetOptionalValue(alertsNode, "WebhookUrl", "/Services/Settings/Alerts/WebhookUrl", errors);

            settings.QuietHoursStart = ParseTimeOfDay(
                GetOptionalValue(alertsNode, "QuietHoursStart", "/Services/Settings/Alerts/QuietHoursStart", errors),
                "/Services/Settings/Alerts/QuietHoursStart",
                errors);

            settings.QuietHoursEnd = ParseTimeOfDay(
                GetOptionalValue(alertsNode, "QuietHoursEnd", "/Services/Settings/Alerts/QuietHoursEnd", errors),
                "/Services/Settings/Alerts/QuietHoursEnd",
                errors);

            if (settings.WebhookEnabled && string.IsNullOrWhiteSpace(settings.WebhookUrl))
            {
                errors.Add("Webhook alerts are enabled but '/Services/Settings/Alerts/WebhookUrl' is empty.");
            }

            if (settings.QuietHoursStart.HasValue ^ settings.QuietHoursEnd.HasValue)
            {
                errors.Add("Quiet hours require both '/Services/Settings/Alerts/QuietHoursStart' and '/Services/Settings/Alerts/QuietHoursEnd'.");
            }

            if (settings.DefaultEscalationThresholdFailures < settings.DefaultAlertThresholdFailures)
            {
                errors.Add("'/Services/Settings/Alerts/DefaultEscalationThresholdFailures' must be >= DefaultAlertThresholdFailures.");
            }

            return settings;
        }

        private Dictionary<string, IServiceType> ParseServiceItems(XmlNode rootNode, AlertSettings alertSettings, List<string> errors)
        {
            var servType = typeof(ServTypes);
            var parsedItems = new Dictionary<string, IServiceType>(StringComparer.OrdinalIgnoreCase);
            var nodes = rootNode.SelectNodes("Service");

            if (nodes == null)
            {
                return parsedItems;
            }

            var index = 0;
            foreach (XmlNode node in nodes)
            {
                index++;
                var pathPrefix = $"/Services/Service[{index}]";

                var name = GetRequiredValue(node, "Name", pathPrefix + "/Name", errors);
                var url = GetRequiredValue(node, "Url", pathPrefix + "/Url", errors);
                var typeValue = GetRequiredValue(node, "Type", pathPrefix + "/Type", errors);

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(typeValue))
                {
                    continue;
                }

                if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    errors.Add($"{pathPrefix}/Url must be an absolute URI.");
                }

                if (!Enum.TryParse(servType, typeValue, ignoreCase: true, out var rawType))
                {
                    errors.Add($"{pathPrefix}/Type '{typeValue}' is not supported. Allowed: HTTP, HTTPS, FTP.");
                    continue;
                }

                var type = (ServTypes)rawType;
                IServiceType service = null;
                switch (type)
                {
                    case ServTypes.HTTP:
                    case ServTypes.HTTPS:
                        service = new HttpService();
                        break;
                    case ServTypes.FTP:
                        service = new FtpService();
                        break;
                }

                if (service == null)
                {
                    errors.Add($"{pathPrefix}/Type '{typeValue}' could not be mapped to a service implementation.");
                    continue;
                }

                service.Name = name;
                service.Url = url;
                service.Enabled = ParseBool(
                    ResolveEnvironmentPlaceholders(XmlUtil.GetValue(node, "Enabled"), pathPrefix + "/Enabled", errors),
                    defaultValue: true,
                    path: pathPrefix + "/Enabled",
                    errors: errors);
                service.Content = GetOptionalValue(node, "Content", pathPrefix + "/Content", errors);
                service.Username = GetOptionalValue(node, "Username", pathPrefix + "/Username", errors);
                service.Password = GetOptionalValue(node, "Password", pathPrefix + "/Password", errors);
                service.Interval = ParseNonNegativeInt(
                    GetOptionalValue(node, "Interval", pathPrefix + "/Interval", errors),
                    pathPrefix + "/Interval",
                    defaultValue: 0,
                    errors: errors,
                    required: false);
                service.ToEmails = GetOptionalValue(node, "ToEmails", pathPrefix + "/ToEmails", errors);
                service.ToNumbers = GetOptionalValue(node, "ToNumbers", pathPrefix + "/ToNumbers", errors);
                service.EnableSms = ParseBool(
                    ResolveEnvironmentPlaceholders(XmlUtil.GetValue(node, "EnableSms"), pathPrefix + "/EnableSms", errors),
                    defaultValue: true,
                    path: pathPrefix + "/EnableSms",
                    errors: errors);
                service.AllowInsecureTls = ParseBool(
                    ResolveEnvironmentPlaceholders(XmlUtil.GetValue(node, "AllowInsecureTls"), pathPrefix + "/AllowInsecureTls", errors),
                    defaultValue: false,
                    path: pathPrefix + "/AllowInsecureTls",
                    errors: errors);

                service.AlertThresholdFailures = ParsePositiveInt(
                    GetOptionalValue(node, "AlertThresholdFailures", pathPrefix + "/AlertThresholdFailures", errors),
                    pathPrefix + "/AlertThresholdFailures",
                    alertSettings.DefaultAlertThresholdFailures,
                    errors,
                    required: false);

                service.AlertCooldownSeconds = ParseNonNegativeInt(
                    GetOptionalValue(node, "AlertCooldownSeconds", pathPrefix + "/AlertCooldownSeconds", errors),
                    pathPrefix + "/AlertCooldownSeconds",
                    alertSettings.DefaultAlertCooldownSeconds,
                    errors,
                    required: false);

                service.EscalationThresholdFailures = ParsePositiveInt(
                    GetOptionalValue(node, "EscalationThresholdFailures", pathPrefix + "/EscalationThresholdFailures", errors),
                    pathPrefix + "/EscalationThresholdFailures",
                    alertSettings.DefaultEscalationThresholdFailures,
                    errors,
                    required: false);

                service.EscalationCooldownSeconds = ParseNonNegativeInt(
                    GetOptionalValue(node, "EscalationCooldownSeconds", pathPrefix + "/EscalationCooldownSeconds", errors),
                    pathPrefix + "/EscalationCooldownSeconds",
                    alertSettings.DefaultEscalationCooldownSeconds,
                    errors,
                    required: false);

                if (service.EscalationThresholdFailures < service.AlertThresholdFailures)
                {
                    errors.Add($"{pathPrefix}/EscalationThresholdFailures must be >= AlertThresholdFailures.");
                }

                if (parsedItems.ContainsKey(service.Name))
                {
                    errors.Add($"Duplicate service name '{service.Name}' at {pathPrefix}/Name. Service names must be unique.");
                    continue;
                }

                parsedItems.Add(service.Name, service);
            }

            return parsedItems;
        }

        private static string GetRequiredValue(XmlNode node, string key, string path, List<string> errors)
        {
            var value = ResolveEnvironmentPlaceholders(XmlUtil.GetValue(node, key), path, errors);
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Missing required value '{path}'.");
            }

            return value;
        }

        private static string GetOptionalValue(XmlNode node, string key, string path, List<string> errors)
        {
            return ResolveEnvironmentPlaceholders(XmlUtil.GetValue(node, key), path, errors);
        }

        private static string ResolveEnvironmentPlaceholders(string value, string path, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var resolved = EnvParenPattern.Replace(value, match => ResolveEnvironmentToken(match, path, errors));
            resolved = EnvBracePattern.Replace(resolved, match => ResolveEnvironmentToken(match, path, errors));
            return resolved;
        }

        private static string ResolveEnvironmentToken(Match match, string path, List<string> errors)
        {
            var tokenName = match.Groups["name"].Value;
            var variableValue = Environment.GetEnvironmentVariable(tokenName);
            if (variableValue != null)
            {
                return variableValue;
            }

            errors.Add($"Environment variable '{tokenName}' referenced by '{path}' is not set.");
            return string.Empty;
        }

        private static bool ParseBool(string value, bool defaultValue, string path, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (value == "1")
            {
                return true;
            }

            if (value == "0")
            {
                return false;
            }

            if (bool.TryParse(value, out var parsed))
            {
                return parsed;
            }

            errors.Add($"'{path}' has invalid boolean value '{value}'.");
            return defaultValue;
        }

        private static int ParsePositiveInt(string value, string path, int defaultValue, List<string> errors, bool required)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (required)
                {
                    errors.Add($"Missing required value '{path}'.");
                }

                return defaultValue;
            }

            if (!int.TryParse(value, out var parsed) || parsed <= 0)
            {
                errors.Add($"'{path}' must be a positive integer.");
                return defaultValue;
            }

            return parsed;
        }

        private static int ParseNonNegativeInt(string value, string path, int defaultValue, List<string> errors, bool required)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (required)
                {
                    errors.Add($"Missing required value '{path}'.");
                }

                return defaultValue;
            }

            if (!int.TryParse(value, out var parsed) || parsed < 0)
            {
                errors.Add($"'{path}' must be a non-negative integer.");
                return defaultValue;
            }

            return parsed;
        }

        private static TimeSpan? ParseTimeOfDay(string value, string path, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var formats = new[] { "hh\\:mm", "h\\:mm", "hh\\:mm\\:ss", "h\\:mm\\:ss" };
            if (TimeSpan.TryParseExact(value, formats, CultureInfo.InvariantCulture, out var parsed))
            {
                if (parsed >= TimeSpan.FromDays(1) || parsed < TimeSpan.Zero)
                {
                    errors.Add($"'{path}' must be between 00:00 and 23:59:59.");
                    return null;
                }

                return parsed;
            }

            errors.Add($"'{path}' has invalid time format '{value}'. Use HH:mm (24-hour)." );
            return null;
        }

        private static string EnsureConfigFile(string configPath)
        {
            if (File.Exists(configPath))
            {
                return configPath;
            }

            var configDirectory = Path.GetDirectoryName(configPath);
            if (string.IsNullOrWhiteSpace(configDirectory))
            {
                configDirectory = Environment.CurrentDirectory;
            }

            var sampleFileName = $"{Path.GetFileNameWithoutExtension(configPath)}.sample{Path.GetExtension(configPath)}";
            var samplePath = Path.Combine(configDirectory, sampleFileName);

            if (!File.Exists(samplePath))
            {
                throw new FileNotFoundException(
                    $"Configuration file '{configPath}' was not found and sample '{samplePath}' is unavailable.");
            }

            File.Copy(samplePath, configPath, overwrite: false);
            Console.WriteLine(
                "[{0:yyyy-MM-dd HH:mm:ss}] Bootstrapped {1} from {2}. Update placeholder values before production use.",
                DateTime.Now,
                Path.GetFileName(configPath),
                sampleFileName);

            return configPath;
        }

        public Dictionary<string, IServiceType> Items { get; set; }
        public int Interval { get; set; }
        public MailSettings MailSettings { get; set; }
        public SmsSettings SmsSettings { get; set; }
        public AlertSettings AlertSettings { get; set; }

        private static ServManager _instance;
        public static ServManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ServManager();
                }

                return _instance;
            }
        }

        public static ServResponse Execute(IServiceType item)
        {
            return item.Execute();
        }
    }
}
