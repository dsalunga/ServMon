using System;
namespace ServMon
{
    interface IServiceType
    {
        string Content { get; set; }
        bool Enabled { get; set; }
        bool EnableSms { get; set; }
        ServResponse Execute();
        string Name { get; set; }
        ServTypes Type { get; set; }
        string Url { get; set; }

        string Username { get; set; }
        string Password { get; set; }
        int Interval { get; set; }
        string ToEmails { get; set; }
        string ToNumbers { get; set; }
        int AlertThresholdFailures { get; set; }
        int AlertCooldownSeconds { get; set; }
        int EscalationThresholdFailures { get; set; }
        int EscalationCooldownSeconds { get; set; }
        DateTime LastUpdate { get; set; }
        bool Success { get; set; }
        string Message { get; set; }
        bool AllowInsecureTls { get; set; }
        int CheckCount { get; set; }
        int SuccessCount { get; set; }
        int FailureCount { get; set; }
        int ConsecutiveFailures { get; set; }
        long LastDurationMs { get; set; }
        double AverageDurationMs { get; set; }
    }
}
