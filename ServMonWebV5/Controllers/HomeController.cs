using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ServMonWebV4.Models;
using ServMonWebV5.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WCMS.Common.Utilities;

namespace ServMonWebV5.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration configuration;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            this.configuration = configuration;
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }



        public ActionResult Index(string go = "")
        {
            go = go.ToLower();
            switch (go)
            {
                case "start":
                    {
                        var ms = Process.GetProcessesByName(configuration.GetSection("appSettings").GetSection("ServMon:ProcessName").Value);
                        if (ms.Length > 0)
                            foreach (var process in ms)
                                process.Kill(true);

                        var processPath = configuration.GetSection("appSettings").GetSection("ServMon:ExecutablePath").Value;
                        var m = new Process();
                        //m.StartInfo.Arguments = "/interactive";
                        //m.StartInfo.RedirectStandardInput = true;
                        //m.StartInfo.UseShellExecute = false;
                        m.StartInfo.FileName = processPath;
                        m.StartInfo.WorkingDirectory = FileHelper.GetFolder(processPath, '\\');
                        m.Start();

                        return RedirectToAction("Index", "Home");
                    }
                case "terminate":
                    {
                        var processes = Process.GetProcessesByName(configuration.GetSection("appSettings").GetSection("ServMon:ProcessName").Value);
                        if (processes.Length > 0)
                            foreach (Process process in processes)
                                process.Kill(true);
                        return RedirectToAction("Index", "Home");
                    }
                default:
                    {
                        var jsonPath = configuration.GetSection("appSettings").GetSection("ServMon:ServicesJsonPath").Value; //ConfigHelper.Get("ServMon:ServicesJsonPath");
                        //jsonPath = WebHelper.MapPath(jsonPath, true);
                        var jsonContent = FileHelper.ReadFile(jsonPath);
                        if (!String.IsNullOrEmpty(jsonContent))
                        {
                            var json = JObject.Parse(jsonContent);
                            ViewBag.Services = json;
                        }

                        // Process status
                        var ms = Process.GetProcessesByName(configuration.GetSection("appSettings").GetSection("ServMon:ProcessName").Value);
                        ViewBag.ServMonRunning = ms.Length > 0;
                        break;
                    }
            }

            ViewBag.EnableEditConfig = true; // DataHelper.GetBool(ConfigHelper.Get("ServMon:EnableEditConfig"), false);
            return View();
        }

        public ActionResult EditConfig()
        {
            var configPath = configuration.GetSection("appSettings").GetSection("ServMon:ConfigPath").Value;
            configPath = FileHelper.EvalPath(configPath, false);

            var model = new EditConfigViewModel();
            model.Content = FileHelper.ReadFile(configPath);

            return View(model);
        }

        //
        // POST: /Account/ForgotPassword
        [HttpPost]
        //[ValidateInput(false)]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditConfig(EditConfigViewModel model)
        {
            if (ModelState.IsValid)
            {
                //var user = await UserManager.FindByNameAsync(model.Email);
                //if (user == null || !(await UserManager.IsEmailConfirmedAsync(user.Id)))
                //{
                //    ModelState.AddModelError("", "The user either does not exist or is not confirmed.");
                //    return View(model);
                //}

                var configPath = configuration.GetSection("appSettings").GetSection("ServMon:ConfigPath").Value;
                configPath = FileHelper.EvalPath(configPath, false);

                System.IO.File.Copy(configPath, configPath + ".bak", true);
                FileHelper.WriteFile(model.Content, configPath, Encoding.UTF8);

                return RedirectToAction("Index", "Home");

                // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=320771
                // Send an email with this link
                // string code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
                // var callbackUrl = Url.Action("ResetPassword", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);		
                // await UserManager.SendEmailAsync(user.Id, "Reset Password", "Please reset your password by clicking <a href=\"" + callbackUrl + "\">here</a>");
                // return RedirectToAction("ForgotPasswordConfirmation", "Account");
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }
    }
}
