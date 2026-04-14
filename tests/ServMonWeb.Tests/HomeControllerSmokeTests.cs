using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using ServMonWeb.Controllers;
using ServMonWeb.Models;
using Xunit;

namespace ServMonWeb.Tests;

public class HomeControllerSmokeTests
{
    [Fact]
    public void ServicesCrudAndDashboardRefresh_SmokeFlowPasses()
    {
        using var sandbox = new Sandbox();
        var configPath = Path.Combine(sandbox.RootPath, "config.xml");
        var samplePath = Path.Combine(sandbox.RootPath, "config.sample.xml");
        var runtimePath = Path.Combine(sandbox.RootPath, "services.json");

        File.WriteAllText(samplePath, BuildConfigXml("Service A", "https://service-a.example.com/"));

        var controller = CreateController(configPath, runtimePath, sandbox.RootPath);

        var servicesResult = Assert.IsType<ViewResult>(controller.Services());
        var servicesModel = Assert.IsType<ServiceConfigListViewModel>(servicesResult.Model);
        Assert.Single(servicesModel.Services);
        Assert.True(File.Exists(configPath));

        var editGet = Assert.IsType<ViewResult>(controller.EditService(0));
        var editModel = Assert.IsType<ServiceConfigEditViewModel>(editGet.Model);
        editModel.Url = "https://service-a.example.com/health";
        editModel.AlertThresholdFailures = 2;
        editModel.EscalationThresholdFailures = 4;

        var editPost = Assert.IsType<RedirectToActionResult>(controller.EditService(editModel));
        Assert.Equal("Services", editPost.ActionName);

        var postEditServices = Assert.IsType<ViewResult>(controller.Services());
        var postEditModel = Assert.IsType<ServiceConfigListViewModel>(postEditServices.Model);
        Assert.Equal("https://service-a.example.com/health", postEditModel.Services[0].Url);
        Assert.Equal(2, postEditModel.Services[0].AlertThresholdFailures);
        Assert.Equal(4, postEditModel.Services[0].EscalationThresholdFailures);

        var addModel = new ServiceConfigEditViewModel
        {
            Name = "Service B",
            Url = "https://service-b.example.com/",
            Type = ServiceTypeOptions.Https,
            Enabled = true,
            Content = "OK",
            Interval = 30,
            ToEmails = "ops@example.com",
            EnableSms = false,
            AllowInsecureTls = false,
            AlertThresholdFailures = 1,
            AlertCooldownSeconds = 300,
            EscalationThresholdFailures = 3,
            EscalationCooldownSeconds = 900
        };

        var addPost = Assert.IsType<RedirectToActionResult>(controller.AddService(addModel));
        Assert.Equal("Services", addPost.ActionName);

        var postAddServices = Assert.IsType<ViewResult>(controller.Services());
        var postAddModel = Assert.IsType<ServiceConfigListViewModel>(postAddServices.Model);
        Assert.Equal(2, postAddModel.Services.Count);

        var addedService = postAddModel.Services.Find(s => s.Name == "Service B");
        Assert.NotNull(addedService);

        var deletePost = Assert.IsType<RedirectToActionResult>(controller.DeleteService(addedService!.Id));
        Assert.Equal("Services", deletePost.ActionName);

        var postDeleteServices = Assert.IsType<ViewResult>(controller.Services());
        var postDeleteModel = Assert.IsType<ServiceConfigListViewModel>(postDeleteServices.Model);
        Assert.Single(postDeleteModel.Services);

        var runtime = JObject.Parse(@"{
  'services': [
    {
      'name': 'Service A',
      'success': false,
      'lastUpdate': '2026-04-14T08:45:00+08:00',
      'message': 'Request timed out',
      'checkCount': 12,
      'failureCount': 3,
      'consecutiveFailures': 2,
      'lastDurationMs': 250,
      'averageDurationMs': 133.33
    }
  ]
}".Replace('\'', '"'));
        File.WriteAllText(runtimePath, runtime.ToString());

        var indexResult = Assert.IsType<ViewResult>(controller.Index());
        var dashboard = Assert.IsType<DashboardViewModel>(indexResult.Model);
        var service = Assert.Single(dashboard.Services);

        Assert.Equal("Service A", service.Name);
        Assert.Equal("https://service-a.example.com/health", service.Url);
        Assert.False(service.RuntimeSuccess);
        Assert.Equal(12, service.CheckCount);
        Assert.Equal(3, service.FailureCount);
        Assert.Equal(2, service.ConsecutiveFailures);
        Assert.Equal(250, service.LastDurationMs);
        Assert.Contains("timed out", service.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MonitoringApiEndpoints_ReturnJsonPayloads()
    {
        using var sandbox = new Sandbox();
        var configPath = Path.Combine(sandbox.RootPath, "config.xml");
        var samplePath = Path.Combine(sandbox.RootPath, "config.sample.xml");
        var runtimePath = Path.Combine(sandbox.RootPath, "services.json");

        File.WriteAllText(samplePath, BuildConfigXml("Service A", "https://service-a.example.com/"));
        File.WriteAllText(runtimePath, "{ \"services\": [] }");

        var controller = CreateController(configPath, runtimePath, sandbox.RootPath);

        var health = Assert.IsType<JsonResult>(controller.MonitoringHealth());
        Assert.NotNull(health.Value);

        var metrics = Assert.IsType<JsonResult>(controller.MonitoringMetrics());
        Assert.NotNull(metrics.Value);
    }

    private static HomeController CreateController(string configPath, string runtimePath, string contentRoot)
    {
        var values = new Dictionary<string, string?>
        {
            ["appSettings:ServMon:ProcessName"] = "ServMon",
            ["appSettings:ServMon:ExecutablePath"] = Path.Combine(contentRoot, "ServMon"),
            ["appSettings:ServMon:ServicesJsonPath"] = runtimePath,
            ["appSettings:ServMon:ConfigPath"] = configPath,
            ["appSettings:ServMon:PidFilePath"] = Path.Combine(contentRoot, "App_Data", "servmon-agent.pid")
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var env = new TestWebHostEnvironment(contentRoot);
        var controller = new HomeController(NullLogger<HomeController>.Instance, configuration, env)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.TempData = new TempDataDictionary(controller.HttpContext, new InMemoryTempDataProvider());
        return controller;
    }

    private static string BuildConfigXml(string serviceName, string url)
    {
        return $@"<?xml version='1.0' encoding='utf-8'?>
<Services>
  <Settings>
    <Interval>30</Interval>
    <Mail>
      <Host>smtp.example.com</Host>
      <Port>587</Port>
      <From>alerts@example.com</From>
      <To>ops@example.com</To>
      <Password>secret</Password>
      <EnableSsl>true</EnableSsl>
      <IsBodyHtml>true</IsBodyHtml>
    </Mail>
    <Sms>
      <Enabled>false</Enabled>
      <To></To>
      <FromNumber></FromNumber>
      <FromName></FromName>
      <Url></Url>
    </Sms>
  </Settings>
  <Service>
    <Name>{serviceName}</Name>
    <Url>{url}</Url>
    <Type>HTTPS</Type>
    <Enabled>1</Enabled>
    <Content>OK</Content>
    <EnableSms>false</EnableSms>
    <AllowInsecureTls>false</AllowInsecureTls>
    <AlertThresholdFailures>1</AlertThresholdFailures>
    <AlertCooldownSeconds>300</AlertCooldownSeconds>
    <EscalationThresholdFailures>5</EscalationThresholdFailures>
    <EscalationCooldownSeconds>900</EscalationCooldownSeconds>
  </Service>
</Services>";
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        private Dictionary<string, object> _values = new();

        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object>(_values);
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            _values = new Dictionary<string, object>(values);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
            WebRootPath = contentRootPath;
            WebRootFileProvider = new PhysicalFileProvider(contentRootPath);
            EnvironmentName = "Development";
            ApplicationName = "ServMonWeb.Tests";
        }

        public string ApplicationName { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
        public string WebRootPath { get; set; }
    }

    private sealed class Sandbox : IDisposable
    {
        public Sandbox()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "servmon-web-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, true);
            }
        }
    }
}
