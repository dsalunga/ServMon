using System;
using System.Collections.Generic;
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
                        while (!token.IsCancellationRequested)
                        {
                            Console.WriteLine("{0}: Started", serv.Name);

                            var success = serv.Success;
                            var response = serv.Execute();
                            if (!response.Success)
                            {
                                if (!string.IsNullOrEmpty(response.Message))
                                    Console.WriteLine("{0}: FAILED. {1}", serv.Name, response.Message);
                                else
                                    Console.WriteLine("{0}: FAILED.", serv.Name);
                                var mail = new MailSender();
                                mail.Subject = string.Format("ServMon: {0} - Check FAILED", serv.Name);
                                mail.Message = string.Format("Service: {0}<br/>Time: {1}<br/>Error: {2}<br/><br/>Trace: {3}", serv.Name, serv.LastUpdate, response.Message, response.StackTrace);
                                mail.To = (ServManager.Instance.MailSettings.To + "," + serv.ToEmails).Trim().TrimEnd(',');
                                mail.Send();
                                Console.WriteLine("{0}: Email sent to {1}", serv.Name, mail.To);

                                var smsTo = (ServManager.Instance.SmsSettings.To + "," + serv.ToNumbers).Trim().TrimEnd(',');
                                if (ServManager.Instance.SmsSettings.Enabled && serv.EnableSms && !string.IsNullOrEmpty(smsTo))
                                {
                                    if (!smsSent)
                                    {
                                        var sms = new SmsSender();
                                        sms.To = smsTo;
                                        sms.Message = string.Format("{0} - FAILED, pls chk. %0AError: {1}", serv.Name, response.Message);
                                        sms.Send();

                                        Console.WriteLine("{0}: SMS sent to {1}", serv.Name, smsTo);
                                        smsSent = true;
                                    }
                                    else
                                    {
                                        smsSent = false;
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("{0}: Success!", serv.Name);
                                smsSent = false;
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
