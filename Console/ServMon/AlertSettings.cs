using System;

namespace ServMon
{
    class AlertSettings
    {
        public int DefaultAlertThresholdFailures { get; set; } = 1;
        public int DefaultAlertCooldownSeconds { get; set; } = 300;
        public int DefaultEscalationThresholdFailures { get; set; } = 5;
        public int DefaultEscalationCooldownSeconds { get; set; } = 900;
        public bool WebhookEnabled { get; set; }
        public string WebhookUrl { get; set; }
        public TimeSpan? QuietHoursStart { get; set; }
        public TimeSpan? QuietHoursEnd { get; set; }
    }
}
