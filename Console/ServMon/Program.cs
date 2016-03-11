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
        /// Sleep interval in minutes.
        /// </summary>
        private static int execInterval;
        private static bool writeState = true;

        static void Main(string[] args)
        {
            var workerThreads = new List<Thread>();

            Console.WriteLine("ServMon started. Press ENTER to stop.");
            Console.WriteLine();

            var schedulerThread = new Thread(() =>
            {
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
                foreach (var item in items)
                {
                    var workerThread = new Thread(() =>
                    {
                        bool smsSent = false; // SMS won't be sent successively, will always skip one instance for consecutive failures.
                        while (true)
                        {
                            var serv = item.Value;
                            //Console.WriteLine();
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
                                mail.Subject = string.Format("MCGI ServMon: {0} - Check FAILED", serv.Name);
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
                                // Change in status, write state to json file
                                writeState = true;
                            }

                            //Console.WriteLine("{0}: Sleeping...", serv.Name);
                            Sleep(serv.Interval);
                        }
                    });

                    workerThreads.Add(workerThread);

                    workerThread.IsBackground = false; // Always dependent to job threads // !forceExecute;
                    workerThread.SetApartmentState(ApartmentState.STA);
                    workerThread.Start();
                }

                var jsonCheck = 0;
                while (true)
                {
                    jsonCheck += 10;
                    Sleep(10); // Check status changes every 10 seconds
                    if (writeState || jsonCheck > execInterval)
                    {
                        // Build Json object
                        var json = new JObject(
                            new JProperty("services",
                                new JArray(
                                        from i in items.Values
                                        select new JObject(
                                            new JProperty("name", i.Name),
                                            new JProperty("lastUpdate", i.LastUpdate),
                                            new JProperty("success", i.Success),
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
            });
            schedulerThread.IsBackground = false; // Always dependent to job threads // !forceExecute;
            schedulerThread.SetApartmentState(ApartmentState.STA);
            schedulerThread.Start();
            Console.ReadLine();

            Console.WriteLine("Aborting Threads. Press wait...");
            // Abort Scheduler Thread
            schedulerThread.Abort();
            foreach (var thread in workerThreads)
                thread.Abort();

            Console.WriteLine();
            Console.WriteLine("Processing stopped. Press ENTER to exit.");
            Console.ReadLine();
        }

        private static void Sleep(int interval = -1)
        {
            Thread.Sleep((interval > 0 ? interval : execInterval) * 1000);
        }
    }
}
