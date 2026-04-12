using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
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

        private const int MaxRetries = 2;
        private const int RetryBaseDelaySeconds = 5;
        private const int AlertCooldownSeconds = 300; // 5 minutes between repeat alerts per service

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
            catch (Exception ex)
            {
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
                        bool smsSent = false;
                        DateTime lastAlertTime = DateTime.MinValue;
                        int consecutiveFailures = 0;

                        while (!token.IsCancellationRequested)
                        {
                            var sw = Stopwatch.StartNew();
                            Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] Check started", DateTime.Now, serv.Type, serv.Name);

                            var success = serv.Success;
                            ServResponse response = null;

                            // Retry with backoff
                            for (int attempt = 0; attempt <= MaxRetries; attempt++)
                            {
                                response = serv.Execute();
                                if (response.Success)
                                    break;

                                if (attempt < MaxRetries)
                                {
                                    var backoffSeconds = RetryBaseDelaySeconds * (attempt + 1);
                                    Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] Retry {3}/{4} after {5}s - {6}",
                                        DateTime.Now, serv.Type, serv.Name, attempt + 1, MaxRetries, backoffSeconds, response.Message);
                                    await SleepAsync(backoffSeconds, token);
                                }
                            }

                            sw.Stop();

                            if (!response.Success)
                            {
                                consecutiveFailures++;
                                Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] FAILED ({3}ms, consecutive={4}). {5}",
                                    DateTime.Now, serv.Type, serv.Name, sw.ElapsedMilliseconds, consecutiveFailures, response.Message);

                                // Alert dedup: only send if cooldown has elapsed
                                var now = DateTime.Now;
                                if ((now - lastAlertTime).TotalSeconds >= AlertCooldownSeconds)
                                {
                                    var mail = new MailSender();
                                    mail.Subject = string.Format("ServMon: {0} - Check FAILED (x{1})", serv.Name, consecutiveFailures);
                                    mail.Message = string.Format("Service: {0}<br/>Type: {1}<br/>Time: {2}<br/>Duration: {3}ms<br/>Consecutive Failures: {4}<br/>Error: {5}<br/><br/>Trace: {6}",
                                        serv.Name, serv.Type, serv.LastUpdate, sw.ElapsedMilliseconds, consecutiveFailures, response.Message, response.StackTrace);
                                    mail.To = (ServManager.Instance.MailSettings.To + "," + serv.ToEmails).Trim().TrimEnd(',');
                                    mail.Send();
                                    Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] Email sent to {3}",
                                        DateTime.Now, serv.Type, serv.Name, mail.To);

                                    var smsTo = (ServManager.Instance.SmsSettings.To + "," + serv.ToNumbers).Trim().TrimEnd(',');
                                    if (ServManager.Instance.SmsSettings.Enabled && serv.EnableSms && !string.IsNullOrEmpty(smsTo))
                                    {
                                        if (!smsSent)
                                        {
                                            var sms = new SmsSender();
                                            sms.To = smsTo;
                                            sms.Message = string.Format("{0} - FAILED (x{1}). {2}", serv.Name, consecutiveFailures, response.Message);
                                            sms.Send();

                                            Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] SMS sent to {3}",
                                                DateTime.Now, serv.Type, serv.Name, smsTo);
                                            smsSent = true;
                                        }
                                        else
                                        {
                                            smsSent = false;
                                        }
                                    }

                                    lastAlertTime = now;
                                }
                                else
                                {
                                    Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] Alert suppressed (cooldown {3}s)",
                                        DateTime.Now, serv.Type, serv.Name, AlertCooldownSeconds);
                                }
                            }
                            else
                            {
                                Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] Success ({3}ms)",
                                    DateTime.Now, serv.Type, serv.Name, sw.ElapsedMilliseconds);
                                smsSent = false;
                                consecutiveFailures = 0;
                            }

                            if (success != response.Success)
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
                        var json = new JObject(
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
                                            new JProperty("message", i.Message)
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
