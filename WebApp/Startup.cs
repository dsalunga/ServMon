using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            // Startup-time configuration validation
            var appSettings = Configuration.GetSection("appSettings");
            var processName = appSettings["ServMon:ProcessName"];
            var executablePath = appSettings["ServMon:ExecutablePath"];
            var servicesJsonPath = appSettings["ServMon:ServicesJsonPath"];

            if (string.IsNullOrWhiteSpace(processName))
                throw new InvalidOperationException("Configuration error: appSettings:ServMon:ProcessName is required.");
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new InvalidOperationException("Configuration error: appSettings:ServMon:ExecutablePath is required.");
            if (string.IsNullOrWhiteSpace(servicesJsonPath))
                throw new InvalidOperationException("Configuration error: appSettings:ServMon:ServicesJsonPath is required.");

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
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
            });

            services.AddControllersWithViews();
            services.AddHealthChecks();
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

            using (var scope = app.ApplicationServices.CreateScope())
            {
                SeedAdminRoleAndUserAsync(scope.ServiceProvider, Configuration).GetAwaiter().GetResult();
            }

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
                endpoints.MapHealthChecks("/health");
            });
        }

        private static async Task SeedAdminRoleAndUserAsync(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var logger = serviceProvider.GetService<ILogger<Startup>>();

            const string adminRole = "Admin";
            if (!await roleManager.RoleExistsAsync(adminRole))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(adminRole));
                if (!roleResult.Succeeded)
                {
                    logger?.LogError("Failed to create role {Role}: {Errors}", adminRole, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
                    return;
                }
            }

            var bootstrapSection = configuration.GetSection("BootstrapAdmin");
            var bootstrapEnabled = bool.TryParse(bootstrapSection["Enabled"], out var parsedEnabled) && parsedEnabled;
            if (!bootstrapEnabled)
            {
                return;
            }

            var adminEmail = bootstrapSection["Email"];
            var adminPassword = bootstrapSection["Password"];
            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                logger?.LogWarning("BootstrapAdmin is enabled but Email/Password is not configured.");
                return;
            }

            var user = await userManager.FindByEmailAsync(adminEmail);
            if (user == null)
            {
                user = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(user, adminPassword);
                if (!createResult.Succeeded)
                {
                    logger?.LogError("Failed to create bootstrap admin user {Email}: {Errors}", adminEmail, string.Join("; ", createResult.Errors.Select(e => e.Description)));
                    return;
                }
            }

            if (!await userManager.IsInRoleAsync(user, adminRole))
            {
                var addRoleResult = await userManager.AddToRoleAsync(user, adminRole);
                if (!addRoleResult.Succeeded)
                {
                    logger?.LogError("Failed to assign role {Role} to {Email}: {Errors}", adminRole, adminEmail, string.Join("; ", addRoleResult.Errors.Select(e => e.Description)));
                }
            }
        }
    }
}
