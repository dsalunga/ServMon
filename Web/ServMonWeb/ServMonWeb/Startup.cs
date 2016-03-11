using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(ServMonWeb.Startup))]
namespace ServMonWeb
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
