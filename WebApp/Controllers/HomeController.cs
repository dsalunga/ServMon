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

            if (!IsAllowedExecutableLocation(processPath))
            {
                _logger.LogError("Agent executable path is outside allowed project root: {Path}", processPath);
                TempData["Error"] = "Agent executable path is outside the allowed project root.";
                return RedirectToAction("Index");
            }

            if (!IsExecutableFile(processPath))
            {
                _logger.LogError("Configured agent file is not executable: {Path}", processPath);
                TempData["Error"] = "Configured agent file is not executable.";
                return RedirectToAction("Index");
            }

            var terminated = TerminateManagedProcesses(processName, processPath);
            if (terminated > 0)
            {
                _logger.LogInformation("Stopped {Count} managed agent process(es) before start.", terminated);
            }

            var m = new Process();
            m.StartInfo.FileName = processPath;
            m.StartInfo.WorkingDirectory = Path.GetDirectoryName(processPath) ?? Environment.CurrentDirectory;
            m.StartInfo.UseShellExecute = false;
            if (!m.Start())
            {
                _logger.LogError("Failed to start agent process from path: {Path}", processPath);
                TempData["Error"] = "Failed to start agent process.";
                return RedirectToAction("Index");
            }

            WriteManagedPid(m.Id);

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public ActionResult Terminate()
        {
            var processName = configuration.GetSection("appSettings").GetSection("ServMon:ProcessName").Value;

            var processPath = ResolveConfiguredPath(configuration.GetSection("appSettings").GetSection("ServMon:ExecutablePath").Value);
            var terminated = TerminateManagedProcesses(processName, processPath);
            _logger.LogInformation("Stopped {Count} managed agent process(es).", terminated);

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
        public ActionResult EditConfig(EditConfigViewModel model)
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

        private int TerminateManagedProcesses(string processName, string expectedPath)
        {
            var terminated = 0;

            if (TryReadManagedPid(out var pid))
            {
                try
                {
                    var process = Process.GetProcessById(pid);
                    if (IsManagedServMonProcess(process, processName, expectedPath))
                    {
                        process.Kill(true);
                        terminated++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Managed PID was not killable: {Pid}", pid);
                }
            }

            foreach (var process in Process.GetProcessesByName(processName))
            {
                if (!IsManagedServMonProcess(process, processName, expectedPath))
                {
                    continue;
                }

                try
                {
                    process.Kill(true);
                    terminated++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to kill managed process {Pid}", process.Id);
                }
            }

            ClearManagedPid();
            return terminated;
        }

        private bool IsManagedServMonProcess(Process process, string expectedName, string expectedPath)
        {
            if (process == null || process.HasExited)
            {
                return false;
            }

            if (!string.Equals(process.ProcessName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                var actualPath = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(actualPath))
                {
                    return false;
                }

                var expectedFullPath = Path.GetFullPath(expectedPath);
                var actualFullPath = Path.GetFullPath(actualPath);
                return string.Equals(actualFullPath, expectedFullPath, GetPathComparison());
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to inspect process executable path for PID {Pid}", process.Id);
                return false;
            }
        }

        private bool IsAllowedExecutableLocation(string processPath)
        {
            var fullPath = Path.GetFullPath(processPath);
            var repoRoot = Directory.GetParent(hostEnvironment.ContentRootPath)?.FullName;
            if (string.IsNullOrWhiteSpace(repoRoot))
            {
                repoRoot = hostEnvironment.ContentRootPath;
            }

            var rootWithSeparator = Path.GetFullPath(repoRoot) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(rootWithSeparator, GetPathComparison());
        }

        private static bool IsExecutableFile(string processPath)
        {
            if (!System.IO.File.Exists(processPath))
            {
                return false;
            }

            if (OperatingSystem.IsWindows())
            {
                var ext = Path.GetExtension(processPath);
                return string.IsNullOrEmpty(ext) || string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase);
            }

            try
            {
                var mode = System.IO.File.GetUnixFileMode(processPath);
                return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
            }
            catch
            {
                return true;
            }
        }

        private string GetPidFilePath()
        {
            var configured = configuration.GetSection("appSettings").GetSection("ServMon:PidFilePath").Value;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return ResolveConfiguredPath(configured);
            }

            return Path.Combine(hostEnvironment.ContentRootPath, "App_Data", "servmon-agent.pid");
        }

        private void WriteManagedPid(int pid)
        {
            try
            {
                var path = GetPidFilePath();
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                System.IO.File.WriteAllText(path, pid.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to write managed PID file.");
            }
        }

        private bool TryReadManagedPid(out int pid)
        {
            pid = 0;
            try
            {
                var path = GetPidFilePath();
                if (!System.IO.File.Exists(path))
                {
                    return false;
                }

                var text = System.IO.File.ReadAllText(path).Trim();
                return int.TryParse(text, out pid);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to read managed PID file.");
                return false;
            }
        }

        private void ClearManagedPid()
        {
            try
            {
                var path = GetPidFilePath();
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to clear managed PID file.");
            }
        }

        private static StringComparison GetPathComparison()
        {
            return OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
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
