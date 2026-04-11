using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ServMonWeb.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WCMS.Common.Utilities;

namespace ServMonWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment hostEnvironment;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration, IWebHostEnvironment hostEnvironment)
        {
            _logger = logger;
            this.configuration = configuration;
            this.hostEnvironment = hostEnvironment;
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

                        var processPath = ResolveConfiguredPath(configuration.GetSection("appSettings").GetSection("ServMon:ExecutablePath").Value);
                        var m = new Process();
                        m.StartInfo.FileName = processPath;
                        m.StartInfo.WorkingDirectory = Path.GetDirectoryName(processPath) ?? Environment.CurrentDirectory;
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
                        var jsonPath = ResolveConfiguredPath(configuration.GetSection("appSettings").GetSection("ServMon:ServicesJsonPath").Value); //ConfigHelper.Get("ServMon:ServicesJsonPath");
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
            var configPath = ResolveConfiguredPath(configuration.GetSection("appSettings").GetSection("ServMon:ConfigPath").Value);
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

                var configPath = ResolveConfiguredPath(configuration.GetSection("appSettings").GetSection("ServMon:ConfigPath").Value);
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

        private string ResolveConfiguredPath(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath;
            }

            if (Path.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            var candidates = new List<string>();
            var contentRoot = hostEnvironment.ContentRootPath;
            if (!string.IsNullOrWhiteSpace(contentRoot))
            {
                candidates.Add(Path.GetFullPath(Path.Combine(contentRoot, configuredPath)));

                var parent = Directory.GetParent(contentRoot)?.FullName;
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    candidates.Add(Path.GetFullPath(Path.Combine(parent, configuredPath)));
                }
            }

            candidates.Add(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, configuredPath)));

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (System.IO.File.Exists(candidate) || Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return candidates.First();
        }
    }
}
