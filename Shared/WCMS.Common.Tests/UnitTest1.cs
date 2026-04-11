using System;
using System.IO;
using System.Text;
using System.Xml;
using WCMS.Common;
using WCMS.Common.Utilities;
using Xunit;

namespace WCMS.Common.Tests;

public class UnitTest1
{
    [Fact]
    public void DataUtil_GetInt32_ParsesAndFallsBack()
    {
        Assert.Equal(42, DataUtil.GetInt32("42", 0));
        Assert.Equal(7, DataUtil.GetInt32("not-a-number", 7));
        Assert.Equal(1, DataUtil.GetInt32(true, 0));
    }

    [Fact]
    public void DataUtil_GetBool_ParsesCommonValues()
    {
        Assert.True(DataUtil.GetBool("1", false));
        Assert.False(DataUtil.GetBool("false", true));
        Assert.True(DataUtil.GetBool((string)null, true));
    }

    [Fact]
    public void XmlUtil_GetValue_ReadsChildAndAttribute()
    {
        var xml = "<Root attr=\"yes\"><Interval>15</Interval><Mail><Host>smtp.example.com</Host></Mail></Root>";
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var root = doc.DocumentElement;

        Assert.Equal("15", XmlUtil.GetValue(root, "Interval"));
        Assert.Equal("smtp.example.com", XmlUtil.GetValue(root, "Mail/Host"));
        Assert.Equal("yes", XmlUtil.GetValue(root, "attr"));
        Assert.Equal("yes", XmlUtil.GetValue(root, "@attr"));
    }

    [Fact]
    public void Substituter_Substitute_UsesNamedValueProvider()
    {
        var values = new NamedValueProvider();
        values.Add("From", "SrvMon");
        values.Add("Number", "6590001111");
        values.Add("Message", "Server down");
        values.Add("Today", "2026-04-12");

        var result = Substituter.Substitute(
            "from=$(From)&to=$(Number)&text=$(Message)&date=$(Today|System.DateTime|yyyy-MM-dd)",
            values);

        Assert.Equal("from=SrvMon&to=6590001111&text=Server down&date=2026-04-12", result);
    }

    [Fact]
    public void FileHelper_WriteReadAndGetFolder_Works()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "servmon-wcms-tests", Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(tempRoot, "services.json");

        try
        {
            Assert.True(FileHelper.WriteFile("{\"ok\":true}", filePath, Encoding.UTF8));
            Assert.Equal("{\"ok\":true}", FileHelper.ReadFile(filePath));
            Assert.Equal(tempRoot, FileHelper.GetFolder(filePath));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public void LogHelper_WriteLog_WritesToSpecificFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "servmon-wcms-tests", Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "custom.log");

        try
        {
            LogHelper.WriteLog(logPath, "Error {0}", 123);
            Assert.True(File.Exists(logPath));
            Assert.Contains("Error 123", File.ReadAllText(logPath));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}
