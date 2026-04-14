using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WCMS.Common.Utilities;

namespace ServMon
{
    class Program
    {
        /// <summary>
        /// Sleep interval in seconds.
        /// </summary>
        private static int execInterval;
        private static volatile bool writeState = true;
        private static readonly DateTimeOffset AgentStartedAtUtc = DateTimeOffset.UtcNow;

        private const int MaxRetries = 2;
        private const int RetryBaseDelaySeconds = 5;

        static async Task Main(string[] args)
        {
            using var cts = new CancellationTokenSource();
            var token = cts.Token;

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            Console.WriteLine("ServMon started. Press Ctrl+C to stop.");
            Console.WriteLine();

            try
            {
                ServManager.Instance.ReadConfig();
            }
            catch (ConfigValidationException vex)
            {
                Console.WriteLine(vex.Message);
                LogHelper.WriteLog(true, vex);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Startup failed: {ex.Message}");
                LogHelper.WriteLog(true, ex);
                return;
            }

            execInterval = ServManager.Instance.Interval;

            Console.WriteLine("Default exec interval: {0} seconds", execInterval);
            Console.WriteLine("Begin instrumentation...");
            Console.WriteLine();

            var items = ServManager.Instance.Items;
            var workerTasks = new List<Task>();

            foreach (var item in items)
            {
                if (item.Value.Enabled)
                {
                    var serv = item.Value;
                    var task = Task.Run(async () =>
                    {
                        var lastAlertTime = DateTime.MinValue;
                        var lastEscalationTime = DateTime.MinValue;

                        while (!token.IsCancellationRequested)
                        {
                            var sw = Stopwatch.StartNew();
                            Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] Check started", DateTime.Now, serv.Type, serv.Name);

                            var previousSuccess = serv.Success;
                            ServResponse response = null;

                            // Retry with backoff
                            for (var attempt = 0; attempt <= MaxRetries; attempt++)
                            {
                                response = serv.Execute();
                                if (response.Success)
                                {
                                    break;
                                }

                                if (attempt < MaxRetries)
                                {
                                    var backoffSeconds = RetryBaseDelaySeconds * (attempt + 1);
                                    Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] Retry {3}/{4} after {5}s - {6}",
                                        DateTime.Now, serv.Type, serv.Name, attempt + 1, MaxRetries, backoffSeconds, response.Message);
                                    await SleepAsync(backoffSeconds, token);
                                }
                            }

                            sw.Stop();
                            UpdateMetrics(serv, response, sw.ElapsedMilliseconds);

                            if (!response.Success)
                            {
                                Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] FAILED ({3}ms, consecutive={4}). {5}",
                                    DateTime.Now, serv.Type, serv.Name, sw.ElapsedMilliseconds, serv.ConsecutiveFailures, response.Message);

                                ProcessAlerting(serv, response, sw.ElapsedMilliseconds, ref lastAlertTime, ref lastEscalationTime);
                            }
                            else
                            {
                                Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] Success ({3}ms)",
                                    DateTime.Now, serv.Type, serv.Name, sw.ElapsedMilliseconds);
                            }

                            // Always write the latest runtime state, including metrics and failure counters.
                            writeState = true;

                            if (previousSuccess != response.Success)
                            {
                                writeState = true;
                            }

                            await SleepAsync(serv.Interval, token);
                        }
                    }, token);

                    workerTasks.Add(task);
                }
            }

            // State writer task
            var stateWriterTask = Task.Run(async () =>
            {
                var jsonCheck = 0;
                while (!token.IsCancellationRequested)
                {
                    jsonCheck += 10;
                    await SleepAsync(10, token);
                    if (writeState || jsonCheck > execInterval)
                    {
                        var enabledServices = items.Values.Where(s => s.Enabled).ToList();
                        var failedServices = enabledServices.Where(s => !s.Success).ToList();

                        var json = new JObject(
                            new JProperty("generatedAtUtc", DateTimeOffset.UtcNow),
                            new JProperty("agentStartedAtUtc", AgentStartedAtUtc),
                            new JProperty("summary",
                                new JObject(
                                    new JProperty("serviceCount", items.Count),
                                    new JProperty("enabledServiceCount", enabledServices.Count),
                                    new JProperty("failedServiceCount", failedServices.Count),
                                    new JProperty("healthyServiceCount", enabledServices.Count - failedServices.Count),
                                    new JProperty("totalChecks", items.Values.Sum(x => x.CheckCount)),
                                    new JProperty("totalFailures", items.Values.Sum(x => x.FailureCount)))),
                            new JProperty("services",
                                new JArray(
                                    from i in items.Values
                                    select new JObject(
                                        new JProperty("name", i.Name),
                                        new JProperty("lastUpdate", i.LastUpdate),
                                        new JProperty("success", i.Success),
                                        new JProperty("enabled", i.Enabled),
                                        new JProperty("type", i.Type),
                                        new JProperty("url", i.Url),
                                        new JProperty("message", i.Message),
                                        new JProperty("checkCount", i.CheckCount),
                                        new JProperty("successCount", i.SuccessCount),
                                        new JProperty("failureCount", i.FailureCount),
                                        new JProperty("consecutiveFailures", i.ConsecutiveFailures),
                                        new JProperty("lastDurationMs", i.LastDurationMs),
                                        new JProperty("averageDurationMs", i.AverageDurationMs)
                                    ))));

                        FileHelper.WriteFile(json.ToString(), "services.json");
                        Console.WriteLine("Services json file successfully written.");

                        writeState = false;
                        jsonCheck = 0;
                    }
                }
            }, token);

            workerTasks.Add(stateWriterTask);

            try
            {
                await Task.WhenAll(workerTasks);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }

            Console.WriteLine();
            Console.WriteLine("Processing stopped gracefully.");
        }

        private static void UpdateMetrics(IServiceType service, ServResponse response, long elapsedMs)
        {
            service.CheckCount++;
            service.LastDurationMs = elapsedMs;
            service.AverageDurationMs = service.CheckCount == 1
                ? elapsedMs
                : ((service.AverageDurationMs * (service.CheckCount - 1)) + elapsedMs) / service.CheckCount;

            if (response.Success)
            {
                service.SuccessCount++;
                service.ConsecutiveFailures = 0;
            }
            else
            {
                service.FailureCount++;
                service.ConsecutiveFailures++;
            }
        }

        private static void ProcessAlerting(IServiceType service, ServResponse response, long elapsedMs, ref DateTime lastAlertTime, ref DateTime lastEscalationTime)
        {
            var settings = ServManager.Instance.AlertSettings ?? new AlertSettings();
            var now = DateTime.Now;

            var threshold = Math.Max(1, service.AlertThresholdFailures);
            if (service.ConsecutiveFailures < threshold)
            {
                Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] Alert deferred until failure threshold {3} (current={4}).",
                    now, service.Type, service.Name, threshold, service.ConsecutiveFailures);
                return;
            }

            if (IsInQuietHours(now, settings))
            {
                Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] Alert suppressed during quiet hours.",
                    now, service.Type, service.Name);
                return;
            }

            var escalationThreshold = Math.Max(threshold, service.EscalationThresholdFailures);
            var isEscalation = service.ConsecutiveFailures >= escalationThreshold;
            var cooldownSeconds = isEscalation ? service.EscalationCooldownSeconds : service.AlertCooldownSeconds;
            var lastSendTime = isEscalation ? lastEscalationTime : lastAlertTime;

            if ((now - lastSendTime).TotalSeconds < cooldownSeconds)
            {
                Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] Alert suppressed (cooldown {3}s, escalation={4}).",
                    now, service.Type, service.Name, cooldownSeconds, isEscalation);
                return;
            }

            SendNotifications(service, response, elapsedMs, isEscalation);
            lastAlertTime = now;
            if (isEscalation)
            {
                lastEscalationTime = now;
            }
        }

        private static void SendNotifications(IServiceType service, ServResponse response, long elapsedMs, bool isEscalation)
        {
            var prefix = isEscalation ? "ESCALATED" : "FAILED";
            var subject = $"ServMon: {service.Name} - Check {prefix} (x{service.ConsecutiveFailures})";

            var detailHtml = string.Format(
                "Service: {0}<br/>Type: {1}<br/>Time: {2}<br/>Duration: {3}ms<br/>Consecutive Failures: {4}<br/>Error: {5}<br/><br/>Trace: {6}",
                service.Name,
                service.Type,
                service.LastUpdate,
                elapsedMs,
                service.ConsecutiveFailures,
                response.Message,
                response.StackTrace);

            var detailText = string.Format(
                "Service: {0}\nType: {1}\nTime: {2}\nDuration: {3}ms\nConsecutive Failures: {4}\nError: {5}",
                service.Name,
                service.Type,
                service.LastUpdate,
                elapsedMs,
                service.ConsecutiveFailures,
                response.Message);

            var mailRecipients = JoinRecipients(ServManager.Instance.MailSettings?.To, service.ToEmails);
            if (!string.IsNullOrWhiteSpace(mailRecipients))
            {
                var mail = new MailSender
                {
                    Subject = subject,
                    Message = detailHtml,
                    To = mailRecipients
                };
                mail.Send();
                Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] Email sent to {3}",
                    DateTime.Now, service.Type, service.Name, mailRecipients);
            }

            var smsRecipients = JoinRecipients(ServManager.Instance.SmsSettings?.To, service.ToNumbers);
            if (ServManager.Instance.SmsSettings?.Enabled == true && service.EnableSms && !string.IsNullOrWhiteSpace(smsRecipients))
            {
                var sms = new SmsSender
                {
                    To = smsRecipients,
                    Message = $"{service.Name} - {prefix} (x{service.ConsecutiveFailures}). {response.Message}"
                };
                sms.Send();
                Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] SMS sent to {3}",
                    DateTime.Now, service.Type, service.Name, smsRecipients);
            }

            var alertSettings = ServManager.Instance.AlertSettings;
            if (alertSettings?.WebhookEnabled == true && !string.IsNullOrWhiteSpace(alertSettings.WebhookUrl))
            {
                var webhook = new WebhookSender
                {
                    WebhookUrl = alertSettings.WebhookUrl,
                    Title = subject,
                    Message = detailText
                };
                webhook.Send();
                Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] Webhook alert sent.",
                    DateTime.Now, service.Type, service.Name);
            }
        }

        private static bool IsInQuietHours(DateTime now, AlertSettings settings)
        {
            if (!settings.QuietHoursStart.HasValue || !settings.QuietHoursEnd.HasValue)
            {
                return false;
            }

            var start = settings.QuietHoursStart.Value;
            var end = settings.QuietHoursEnd.Value;
            var current = now.TimeOfDay;

            if (start == end)
            {
                return false;
            }

            if (start < end)
            {
                return current >= start && current < end;
            }

            return current >= start || current < end;
        }

        private static string JoinRecipients(string globalRecipients, string serviceRecipients)
        {
            var all = new[] { globalRecipients, serviceRecipients }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return all.Length == 0 ? string.Empty : string.Join(",", all);
        }

        private static async Task SleepAsync(int interval, CancellationToken token)
        {
            try
            {
                await Task.Delay((interval > 0 ? interval : execInterval) * 1000, token);
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
        }
    }
}
