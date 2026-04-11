using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using WCMS.Common.Utilities;

namespace ServMonWeb
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            if (DataHelper.GetBool(ConfigHelper.Get("ServMon:AgentAutoStart"), false))
            {
                var processes = Process.GetProcessesByName(ConfigHelper.Get("ServMon:ProcessName"));
                if (processes.Length == 0)
                {
                    var processPath = WebHelper.MapPath(ConfigHelper.Get("ServMon:ExecutablePath"), true);
                    var m = new Process();
                    m.StartInfo.FileName = processPath;
                    m.StartInfo.WorkingDirectory = FileHelper.GetFolder(processPath, '\\');
                    m.Start();
                }
            }
        }
    }
}
