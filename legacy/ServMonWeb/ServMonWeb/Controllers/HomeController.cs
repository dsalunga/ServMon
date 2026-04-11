using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServMonWeb.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using WCMS.Common.Utilities;

namespace ServMonWeb.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        public ActionResult Index(string go = "")
        {
            go = go.ToLower();
            switch (go)
            {
                case "start":
                    {
                        var ms = Process.GetProcessesByName(ConfigHelper.Get("ServMon:ProcessName"));
                        if (ms.Length > 0)
                            foreach (var process in ms)
                                process.Kill();

                        var processPath = WebHelper.MapPath(ConfigHelper.Get("ServMon:ExecutablePath"), true);
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
                        var processes = Process.GetProcessesByName(ConfigHelper.Get("ServMon:ProcessName"));
                        if (processes.Length > 0)
                            foreach (Process process in processes)
                                process.Kill();
                        return RedirectToAction("Index", "Home");
                    }
                default:
                    {
                        var jsonPath = ConfigHelper.Get("ServMon:ServicesJsonPath");
                        jsonPath = WebHelper.MapPath(jsonPath, true);
                        var json = JObject.Parse(FileHelper.ReadFile(jsonPath));
                        ViewBag.Services = json;

                        // Process status
                        var ms = Process.GetProcessesByName(ConfigHelper.Get("ServMon:ProcessName"));
                        ViewBag.ServMonRunning = ms.Length > 0;
                        break;
                    }
            }

            ViewBag.EnableEditConfig = DataHelper.GetBool(ConfigHelper.Get("ServMon:EnableEditConfig"), false);
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";
            return View();
        }

        public ActionResult EditConfig()
        {
            var configPath = ConfigHelper.Get("ServMon:ConfigPath");
            configPath = FileHelper.EvalPath(configPath, false);

            var model = new EditConfigViewModel();
            model.Content = FileHelper.ReadFile(configPath);

            return View(model);
        }

        //
        // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateInput(false)]
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

                var configPath = ConfigHelper.Get("ServMon:ConfigPath");
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

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";
            return View();
        }
    }
}