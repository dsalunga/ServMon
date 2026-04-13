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
using System.Xml;
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
            var processName = configuration.GetSection("appSettings").GetSection("ServMon:ProcessName").Value;
            var runningProcesses = Process.GetProcessesByName(processName);
            var runtimeStatuses = ReadRuntimeStatuses();
            var configuredServices = ReadServiceConfigs();

            var model = new DashboardViewModel
            {
                ServMonRunning = runningProcesses.Length > 0,
                EnableEditConfig = true
            };

            foreach (var configured in configuredServices)
            {
                runtimeStatuses.TryGetValue(configured.Name ?? string.Empty, out var runtimeStatus);
                var message = runtimeStatus?.Message;

                if (string.IsNullOrWhiteSpace(message))
                {
                    if (!configured.Enabled)
                    {
                        message = "Disabled";
                    }
                    else if (runtimeStatus == null)
                    {
                        message = model.ServMonRunning ? "Awaiting first check..." : "Agent not running";
                    }
                }

                model.Services.Add(new DashboardServiceViewModel
                {
                    Name = configured.Name,
                    Type = configured.Type,
                    Url = configured.Url,
                    Enabled = configured.Enabled,
                    EnableSms = configured.EnableSms,
                    ToEmails = configured.ToEmails,
                    ToNumbers = configured.ToNumbers,
                    HasRuntimeStatus = runtimeStatus != null,
                    RuntimeSuccess = runtimeStatus?.Success ?? false,
                    LastUpdate = runtimeStatus?.LastUpdate,
                    Message = message
                });
            }

            return View(model);
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

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public ActionResult Services()
        {
            try
            {
                var services = ReadServiceConfigs();
                var model = new ServiceConfigListViewModel
                {
                    Services = services.Select(s => new ServiceConfigListItemViewModel
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Type = s.Type,
                        Url = s.Url,
                        Enabled = s.Enabled,
                        EnableSms = s.EnableSms,
                        AllowInsecureTls = s.AllowInsecureTls,
                        Interval = s.Interval,
                        Content = s.Content,
                        ToEmails = s.ToEmails,
                        ToNumbers = s.ToNumbers
                    }).ToList()
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load service list from config.");
                TempData["Error"] = "Failed to load services from config.xml.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public ActionResult AddService()
        {
            var model = new ServiceConfigEditViewModel
            {
                Enabled = true,
                EnableSms = true,
                Type = ServiceTypeOptions.Http
            };
            return View("EditService", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public ActionResult AddService(ServiceConfigEditViewModel model)
        {
            ValidateServiceModel(model, null);
            if (!ModelState.IsValid)
            {
                return View("EditService", model);
            }

            try
            {
                var configPath = GetConfigPath();
                var document = LoadConfigDocument(configPath);
                var servicesNode = document.SelectSingleNode("/Services");
                if (servicesNode == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid config.xml format. Missing Services root node.");
                    return View("EditService", model);
                }

                var newServiceNode = document.CreateElement("Service");
                servicesNode.AppendChild(newServiceNode);
                ApplyServiceModelToNode(document, newServiceNode, model, preserveExistingPassword: false);
                SaveConfigDocument(document, configPath);

                TempData["Success"] = $"Service '{model.Name}' added.";
                return RedirectToAction(nameof(Services));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add service.");
                ModelState.AddModelError(string.Empty, "Failed to save service changes.");
                return View("EditService", model);
            }
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public ActionResult EditService(int id)
        {
            try
            {
                var service = ReadServiceConfigs().FirstOrDefault(s => s.Id == id);
                if (service == null)
                {
                    TempData["Error"] = "Service entry not found.";
                    return RedirectToAction(nameof(Services));
                }

                var model = new ServiceConfigEditViewModel
                {
                    Id = service.Id,
                    Name = service.Name,
                    Url = service.Url,
                    Type = service.Type,
                    Enabled = service.Enabled,
                    Content = service.Content,
                    Interval = service.Interval,
                    ToEmails = service.ToEmails,
                    ToNumbers = service.ToNumbers,
                    EnableSms = service.EnableSms,
                    Username = service.Username,
                    Password = string.Empty,
                    AllowInsecureTls = service.AllowInsecureTls
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load service for edit. Id={Id}", id);
                TempData["Error"] = "Failed to load service entry.";
                return RedirectToAction(nameof(Services));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public ActionResult EditService(ServiceConfigEditViewModel model)
        {
            if (!model.Id.HasValue)
            {
                TempData["Error"] = "Invalid service id.";
                return RedirectToAction(nameof(Services));
            }

            ValidateServiceModel(model, model.Id.Value);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var configPath = GetConfigPath();
                var document = LoadConfigDocument(configPath);
                var serviceNodes = document.SelectNodes("//Services/Service");
                if (serviceNodes == null || model.Id.Value < 0 || model.Id.Value >= serviceNodes.Count)
                {
                    TempData["Error"] = "Service entry no longer exists.";
                    return RedirectToAction(nameof(Services));
                }

                var targetNode = serviceNodes[model.Id.Value];
                ApplyServiceModelToNode(document, targetNode, model, preserveExistingPassword: true);
                SaveConfigDocument(document, configPath);

                TempData["Success"] = $"Service '{model.Name}' updated.";
                return RedirectToAction(nameof(Services));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update service. Id={Id}", model.Id);
                ModelState.AddModelError(string.Empty, "Failed to save service changes.");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public ActionResult DeleteService(int id)
        {
            try
            {
                var configPath = GetConfigPath();
                var document = LoadConfigDocument(configPath);
                var serviceNodes = document.SelectNodes("//Services/Service");
                if (serviceNodes == null || id < 0 || id >= serviceNodes.Count)
                {
                    TempData["Error"] = "Service entry no longer exists.";
                    return RedirectToAction(nameof(Services));
                }

                var node = serviceNodes[id];
                var serviceName = GetNodeValue(node, "Name");
                node.ParentNode?.RemoveChild(node);
                SaveConfigDocument(document, configPath);

                TempData["Success"] = $"Service '{serviceName}' removed.";
                return RedirectToAction(nameof(Services));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete service. Id={Id}", id);
                TempData["Error"] = "Failed to delete service.";
                return RedirectToAction(nameof(Services));
            }
        }

        private string GetConfigPath()
        {
            var configPath = ResolveConfiguredPath(configuration.GetSection("appSettings").GetSection("ServMon:ConfigPath").Value);
            return FileHelper.EvalPath(configPath, false);
        }

        private sealed class ServiceConfigRecord
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Url { get; set; }
            public string Type { get; set; }
            public bool Enabled { get; set; }
            public string Content { get; set; }
            public int? Interval { get; set; }
            public string ToEmails { get; set; }
            public string ToNumbers { get; set; }
            public bool EnableSms { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public bool AllowInsecureTls { get; set; }
        }

        private sealed class RuntimeServiceStatus
        {
            public bool Success { get; set; }
            public string LastUpdate { get; set; }
            public string Message { get; set; }
        }

        private List<ServiceConfigRecord> ReadServiceConfigs()
        {
            var configPath = GetConfigPath();
            var document = LoadConfigDocument(configPath);
            var nodes = document.SelectNodes("//Services/Service");
            var services = new List<ServiceConfigRecord>();

            if (nodes == null)
            {
                return services;
            }

            var index = 0;
            foreach (XmlNode node in nodes)
            {
                services.Add(new ServiceConfigRecord
                {
                    Id = index++,
                    Name = GetNodeValue(node, "Name"),
                    Url = GetNodeValue(node, "Url"),
                    Type = NormalizeServiceType(GetNodeValue(node, "Type")),
                    Enabled = ParseBool(GetNodeValue(node, "Enabled"), true),
                    Content = GetNodeValue(node, "Content"),
                    Interval = ParseNullableInt(GetNodeValue(node, "Interval")),
                    ToEmails = GetNodeValue(node, "ToEmails"),
                    ToNumbers = GetNodeValue(node, "ToNumbers"),
                    EnableSms = ParseBool(GetNodeValue(node, "EnableSms"), true),
                    Username = GetNodeValue(node, "Username"),
                    Password = GetNodeValue(node, "Password"),
                    AllowInsecureTls = ParseBool(GetNodeValue(node, "AllowInsecureTls"), false)
                });
            }

            return services;
        }

        private Dictionary<string, RuntimeServiceStatus> ReadRuntimeStatuses()
        {
            var statuses = new Dictionary<string, RuntimeServiceStatus>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var jsonPath = ResolveConfiguredPath(configuration.GetSection("appSettings").GetSection("ServMon:ServicesJsonPath").Value);
                var jsonContent = FileHelper.ReadFile(jsonPath);
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    return statuses;
                }

                var json = JObject.Parse(jsonContent);
                var services = json["services"] as JArray;
                if (services == null)
                {
                    return statuses;
                }

                foreach (var token in services.OfType<JObject>())
                {
                    var name = token.Value<string>("name")?.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    statuses[name] = new RuntimeServiceStatus
                    {
                        Success = ParseJTokenBool(token["success"]),
                        LastUpdate = token.Value<string>("lastUpdate"),
                        Message = token.Value<string>("message")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse services runtime status JSON.");
            }

            return statuses;
        }

        private static bool ParseJTokenBool(JToken token, bool defaultValue = false)
        {
            if (token == null)
            {
                return defaultValue;
            }

            if (token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }

            var text = token.ToString();
            if (text == "1")
            {
                return true;
            }

            if (text == "0")
            {
                return false;
            }

            return bool.TryParse(text, out var parsed) ? parsed : defaultValue;
        }

        private void ValidateServiceModel(ServiceConfigEditViewModel model, int? editingId)
        {
            model.Name = (model.Name ?? string.Empty).Trim();
            model.Url = (model.Url ?? string.Empty).Trim();
            model.Type = NormalizeServiceType(model.Type);
            model.Content = (model.Content ?? string.Empty).Trim();
            model.ToEmails = (model.ToEmails ?? string.Empty).Trim();
            model.ToNumbers = (model.ToNumbers ?? string.Empty).Trim();
            model.Username = (model.Username ?? string.Empty).Trim();

            if (!ServiceTypeOptions.Values.Contains(model.Type, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.Type), "Type must be HTTP, HTTPS, or FTP.");
            }

            try
            {
                var existing = ReadServiceConfigs();
                if (existing.Any(s => s.Id != editingId && string.Equals(s.Name, model.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    ModelState.AddModelError(nameof(model.Name), "Service name must be unique.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate service model against existing config.");
                ModelState.AddModelError(string.Empty, "Failed to read existing config for validation.");
            }
        }

        private static string NormalizeServiceType(string value)
        {
            var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
            return string.IsNullOrEmpty(normalized) ? ServiceTypeOptions.Http : normalized;
        }

        private static bool ParseBool(string value, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            var normalized = value.Trim();
            if (normalized == "1")
            {
                return true;
            }

            if (normalized == "0")
            {
                return false;
            }

            return bool.TryParse(normalized, out var parsed) ? parsed : defaultValue;
        }

        private static int? ParseNullableInt(string value)
        {
            if (int.TryParse(value, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static XmlDocument LoadConfigDocument(string configPath)
        {
            var document = new XmlDocument();
            document.Load(configPath);
            return document;
        }

        private static void SaveConfigDocument(XmlDocument document, string configPath)
        {
            System.IO.File.Copy(configPath, configPath + ".bak", true);
            document.Save(configPath);
        }

        private static string GetNodeValue(XmlNode parent, string nodeName)
        {
            return parent.SelectSingleNode(nodeName)?.InnerText?.Trim() ?? string.Empty;
        }

        private static void SetNodeValue(XmlDocument document, XmlNode parent, string nodeName, string value, bool removeIfEmpty)
        {
            var child = parent.SelectSingleNode(nodeName);
            if (string.IsNullOrWhiteSpace(value) && removeIfEmpty)
            {
                if (child != null)
                {
                    parent.RemoveChild(child);
                }

                return;
            }

            if (child == null)
            {
                child = document.CreateElement(nodeName);
                parent.AppendChild(child);
            }

            child.InnerText = value ?? string.Empty;
        }

        private static void ApplyServiceModelToNode(XmlDocument document, XmlNode serviceNode, ServiceConfigEditViewModel model, bool preserveExistingPassword)
        {
            SetNodeValue(document, serviceNode, "Name", model.Name, removeIfEmpty: false);
            SetNodeValue(document, serviceNode, "Url", model.Url, removeIfEmpty: false);
            SetNodeValue(document, serviceNode, "Type", model.Type, removeIfEmpty: false);
            SetNodeValue(document, serviceNode, "Enabled", model.Enabled ? "1" : "0", removeIfEmpty: false);
            SetNodeValue(document, serviceNode, "Content", model.Content, removeIfEmpty: true);
            SetNodeValue(document, serviceNode, "ToEmails", model.ToEmails, removeIfEmpty: true);
            SetNodeValue(document, serviceNode, "ToNumbers", model.ToNumbers, removeIfEmpty: true);
            SetNodeValue(document, serviceNode, "EnableSms", model.EnableSms ? "true" : "false", removeIfEmpty: false);
            SetNodeValue(document, serviceNode, "Username", model.Username, removeIfEmpty: true);

            if (!preserveExistingPassword || !string.IsNullOrWhiteSpace(model.Password))
            {
                SetNodeValue(document, serviceNode, "Password", model.Password, removeIfEmpty: true);
            }

            if (model.Interval.HasValue)
            {
                SetNodeValue(document, serviceNode, "Interval", model.Interval.Value.ToString(), removeIfEmpty: false);
            }
            else
            {
                SetNodeValue(document, serviceNode, "Interval", string.Empty, removeIfEmpty: true);
            }

            SetNodeValue(document, serviceNode, "AllowInsecureTls", model.AllowInsecureTls ? "true" : "false", removeIfEmpty: false);
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
