using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(VehicleSearchWeb.Startup))]
namespace VehicleSearchWeb
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
        }
    }
}
