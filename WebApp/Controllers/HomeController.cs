using Microsoft.AspNetCore.Authorization;
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
    [Authorize]
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
        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }



        [HttpGet]
        public ActionResult Index()
        {
            var jsonPath = ResolveConfiguredPath(configuration.GetSection("appSettings").GetSection("ServMon:ServicesJsonPath").Value);
            var jsonContent = FileHelper.ReadFile(jsonPath);
            if (!String.IsNullOrEmpty(jsonContent))
            {
                var json = JObject.Parse(jsonContent);
                ViewBag.Services = json;
            }

            // Process status
            var ms = Process.GetProcessesByName(configuration.GetSection("appSettings").GetSection("ServMon:ProcessName").Value);
            ViewBag.ServMonRunning = ms.Length > 0;

            ViewBag.EnableEditConfig = true;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public ActionResult Start()
        {
            var processName = configuration.GetSection("appSettings").GetSection("ServMon:ProcessName").Value;
            var processPath = ResolveConfiguredPath(configuration.GetSection("appSettings").GetSection("ServMon:ExecutablePath").Value);

            if (string.IsNullOrWhiteSpace(processPath) || !System.IO.File.Exists(processPath))
            {
                _logger.LogError("Agent executable not found at configured path: {Path}", processPath);
                TempData["Error"] = "Agent executable not found at the configured path.";
                return RedirectToAction("Index");
            }

            var existing = Process.GetProcessesByName(processName);
            foreach (var process in existing)
            {
                try { process.Kill(true); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to kill existing process {Pid}", process.Id); }
            }

            var m = new Process();
            m.StartInfo.FileName = processPath;
            m.StartInfo.WorkingDirectory = Path.GetDirectoryName(processPath) ?? Environment.CurrentDirectory;
            m.Start();

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public ActionResult Terminate()
        {
            var processName = configuration.GetSection("appSettings").GetSection("ServMon:ProcessName").Value;
            var processes = Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                try { process.Kill(true); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to kill process {Pid}", process.Id); }
            }

            return RedirectToAction("Index");
        }

        [Authorize(Policy = "AdminOnly")]
        public ActionResult EditConfig()
        {
            var configPath = ResolveConfiguredPath(configuration.GetSection("appSettings").GetSection("ServMon:ConfigPath").Value);
            configPath = FileHelper.EvalPath(configPath, false);

            var model = new EditConfigViewModel();
            model.Content = FileHelper.ReadFile(configPath);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult> EditConfig(EditConfigViewModel model)
        {
            if (ModelState.IsValid)
            {
                var configPath = ResolveConfiguredPath(configuration.GetSection("appSettings").GetSection("ServMon:ConfigPath").Value);
                configPath = FileHelper.EvalPath(configPath, false);

                System.IO.File.Copy(configPath, configPath + ".bak", true);
                FileHelper.WriteFile(model.Content, configPath, Encoding.UTF8);

                return RedirectToAction("Index", "Home");
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
