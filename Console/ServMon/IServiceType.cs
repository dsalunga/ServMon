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
        DateTime LastUpdate { get; set; }
        bool Success { get; set; }
        string Message { get; set; }
    }
}
