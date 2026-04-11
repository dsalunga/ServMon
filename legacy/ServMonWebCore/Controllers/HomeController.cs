using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using ServMonWebV4.Models;
using System.Diagnostics;
using System.Text;
using WCMS.Common.Utilities;

namespace ServMonWebV4.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration configuration;
        private readonly IConfigurationSection appSettings;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            this.configuration = configuration;
            this.appSettings = configuration.GetSection("appSettings");
        }

        /*public IActionResult Index()
        {
            return View();
        }*/

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public void TerminateProcess()
        {
            var processes = Process.GetProcessesByName(appSettings.GetSection("ServMon:ProcessName").Value);
            if (processes.Length > 0)
                foreach (Process process in processes)
                    process.Kill(true);
        }


        public ActionResult Index(string go = "")
        {
            go = go.ToLower();
            switch (go)
            {
                case "start":
                    {
                        TerminateProcess();

                        var processPath = appSettings.GetSection("ServMon:ExecutablePath").Value;
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
                        TerminateProcess();

                        return RedirectToAction("Index", "Home");
                    }
                default:
                    {
                        var jsonPath = appSettings.GetSection("ServMon:ServicesJsonPath").Value; //ConfigHelper.Get("ServMon:ServicesJsonPath");
                        //jsonPath = WebHelper.MapPath(jsonPath, true);
                        var jsonContent = FileHelper.ReadFile(jsonPath);
                        if (!string.IsNullOrEmpty(jsonContent))
                        {
                            var json = JObject.Parse(jsonContent);
                            ViewBag.Services = json;
                        }

                        // Process status
                        var ms = Process.GetProcessesByName(appSettings.GetSection("ServMon:ProcessName").Value);
                        ViewBag.ServMonRunning = ms.Length > 0;
                        break;
                    }
            }

            ViewBag.EnableEditConfig = true; // DataHelper.GetBool(ConfigHelper.Get("ServMon:EnableEditConfig"), false);
            return View();
        }

        public ActionResult EditConfig()
        {
            var configPath = appSettings.GetSection("ServMon:ConfigPath").Value;
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

                var configPath = appSettings.GetSection("ServMon:ConfigPath").Value;
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