using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ServMonWeb.Models
{
    public class EditConfigViewModel
    {
        [Required]
        [Display(Name = "Content")]
        public string Content { get; set; }
    }

    public static class ServiceTypeOptions
    {
        public const string Http = "HTTP";
        public const string Https = "HTTPS";
        public const string Ftp = "FTP";

        public static readonly IReadOnlyList<string> Values = new[] { Http, Https, Ftp };
    }

    public class ServiceConfigListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        public bool Enabled { get; set; }
        public bool EnableSms { get; set; }
        public bool AllowInsecureTls { get; set; }
        public int? Interval { get; set; }
        public string Content { get; set; }
        public string ToEmails { get; set; }
        public string ToNumbers { get; set; }
        public int AlertThresholdFailures { get; set; }
        public int AlertCooldownSeconds { get; set; }
        public int EscalationThresholdFailures { get; set; }
        public int EscalationCooldownSeconds { get; set; }
    }

    public class ServiceConfigListViewModel
    {
        public List<ServiceConfigListItemViewModel> Services { get; set; } = new List<ServiceConfigListItemViewModel>();
    }

    public class ServiceConfigEditViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Service Name")]
        public string Name { get; set; }

        [Required]
        [Display(Name = "URL")]
        public string Url { get; set; }

        [Required]
        [Display(Name = "Type")]
        public string Type { get; set; } = ServiceTypeOptions.Http;

        [Display(Name = "Enabled")]
        public bool Enabled { get; set; } = true;

        [Display(Name = "Expected Content")]
        public string Content { get; set; }

        [Display(Name = "Interval (seconds, optional)")]
        [Range(0, 604800)]
        public int? Interval { get; set; }

        [Display(Name = "Notify Emails (comma-separated)")]
        public string ToEmails { get; set; }

        [Display(Name = "Notify Numbers (comma-separated)")]
        public string ToNumbers { get; set; }

        [Display(Name = "Enable SMS")]
        public bool EnableSms { get; set; } = true;

        [Display(Name = "FTP Username")]
        public string Username { get; set; }

        [Display(Name = "FTP Password (leave blank to keep existing)")]
        public string Password { get; set; }

        [Display(Name = "Allow Insecure TLS (non-production)")]
        public bool AllowInsecureTls { get; set; }

        [Display(Name = "Alert Threshold (failures)")]
        [Range(1, 9999)]
        public int AlertThresholdFailures { get; set; } = 1;

        [Display(Name = "Alert Cooldown (seconds)")]
        [Range(0, 86400)]
        public int AlertCooldownSeconds { get; set; } = 300;

        [Display(Name = "Escalation Threshold (failures)")]
        [Range(1, 9999)]
        public int EscalationThresholdFailures { get; set; } = 5;

        [Display(Name = "Escalation Cooldown (seconds)")]
        [Range(0, 86400)]
        public int EscalationCooldownSeconds { get; set; } = 900;

        public bool IsNew => !Id.HasValue;
    }

    public class DashboardServiceViewModel
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        public bool Enabled { get; set; }
        public bool EnableSms { get; set; }
        public string ToEmails { get; set; }
        public string ToNumbers { get; set; }
        public bool HasRuntimeStatus { get; set; }
        public bool RuntimeSuccess { get; set; }
        public string LastUpdate { get; set; }
        public string Message { get; set; }
        public int CheckCount { get; set; }
        public int FailureCount { get; set; }
        public int ConsecutiveFailures { get; set; }
        public long LastDurationMs { get; set; }
        public double AverageDurationMs { get; set; }
    }

    public class DashboardViewModel
    {
        public bool ServMonRunning { get; set; }
        public bool EnableEditConfig { get; set; }
        public List<DashboardServiceViewModel> Services { get; set; } = new List<DashboardServiceViewModel>();
    }
}
