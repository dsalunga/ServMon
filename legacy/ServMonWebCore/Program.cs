using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServMonWebV4.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var databaseProvider = (builder.Configuration["DatabaseProvider"] ?? "Postgres").Trim();
var defaultConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var postgresConnectionString = builder.Configuration.GetConnectionString("PostgresConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
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
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
