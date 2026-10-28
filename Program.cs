using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockSense.Data;
using StockSense.Services;

var builder = WebApplication.CreateBuilder(args);

// Local, git-ignored file for the real database password (copy appsettings.Example.json to appsettings.Local.json).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

builder.Services.AddDbContext<StockSenseDbContext>(options =>
    options.UseMySql(connectionString, DbConfig.GetServerVersion(builder.Configuration)));

// Business services
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<ISaleService, SaleService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<ICustomerLedgerService, CustomerLedgerService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IDemoDataService, DemoDataService>();

// Every POST / PUT / DELETE request must carry a valid antiforgery token.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app);

// Indian number / currency formatting and "." as the decimal separator regardless of the Windows region.
var indian = CultureInfo.InvariantCulture;
try { indian = new CultureInfo("en-IN"); } catch (CultureNotFoundException) { }

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(indian),
    SupportedCultures = new List<CultureInfo> { indian },
    SupportedUICultures = new List<CultureInfo> { indian },
    RequestCultureProviders = new List<IRequestCultureProvider>()
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStatusCodePagesWithReExecute("/Home/HttpStatus", "?code={0}");
app.UseStaticFiles();

// If the database could not be reached at startup, show a clear setup page instead of crashing on every request.
app.Use(async (context, next) =>
{
    if (DatabaseStatus.Error != null && !context.Request.Path.StartsWithSegments("/css") &&
        !context.Request.Path.StartsWithSegments("/js"))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "text/html; charset=utf-8";
        var message = WebUtility.HtmlEncode(DatabaseStatus.Error);
        await context.Response.WriteAsync($@"<!doctype html>
<html lang=""en""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>StockSense - database setup needed</title>
<style>
body{{font-family:Segoe UI,Arial,sans-serif;background:#f4f8f6;color:#1f2937;margin:0;padding:24px}}
.card{{max-width:720px;margin:6vh auto;background:#fff;border-radius:16px;padding:32px;box-shadow:0 10px 30px rgba(6,78,59,.12);border-top:6px solid #059669}}
h1{{margin:0 0 8px;color:#065f46;font-size:24px}} code{{background:#ecfdf5;padding:2px 6px;border-radius:6px}}
.err{{background:#fef2f2;border:1px solid #fecaca;color:#991b1b;padding:12px 14px;border-radius:10px;margin:16px 0;word-break:break-word}}
li{{margin:6px 0}}
</style></head><body><div class=""card"">
<h1>StockSense needs a database connection</h1>
<p>The website started, but it could not use the MySQL database.</p>
<div class=""err"">{message}</div>
<ol>
<li>Make sure MySQL is running and the <code>stocksense</code> database exists (<code>Database/create_database.sql</code>).</li>
<li>Copy <code>appsettings.Example.json</code> to <code>appsettings.Local.json</code> and put your real MySQL user and password in it.</li>
<li>Stop the app (Ctrl+C) and run <code>dotnet run</code> again.</li>
</ol>
<p>Full instructions are in <code>README.md</code>.</p>
</div></body></html>");
        return;
    }

    await next();
});

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
