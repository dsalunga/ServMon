using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServMonWeb.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ServMonWeb
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var databaseProvider = (Configuration["DatabaseProvider"] ?? "Postgres").Trim();
            var defaultConnectionString = Configuration.GetConnectionString("DefaultConnection");
            var postgresConnectionString = Configuration.GetConnectionString("PostgresConnection");

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                switch (databaseProvider.ToLowerInvariant())
                {
                    case "postgres":
                    case "postgresql":
                    case "npgsql":
                        if (string.IsNullOrWhiteSpace(postgresConnectionString))
                            throw new InvalidOperationException("DatabaseProvider is set to Postgres but ConnectionStrings:PostgresConnection is missing.");
                        options.UseNpgsql(postgresConnectionString);
                        break;

                    case "sqlserver":
                    case "mssql":
                    case "":
                        if (string.IsNullOrWhiteSpace(defaultConnectionString))
                            throw new InvalidOperationException("DatabaseProvider is set to SqlServer but ConnectionStrings:DefaultConnection is missing.");
                        options.UseSqlServer(defaultConnectionString);
                        break;

                    default:
                        throw new InvalidOperationException($"Unsupported DatabaseProvider '{databaseProvider}'. Supported values: SqlServer, Postgres.");
                }
            });
            services.AddDatabaseDeveloperPageExceptionFilter();

            services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddControllersWithViews();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });
        }
    }
}
