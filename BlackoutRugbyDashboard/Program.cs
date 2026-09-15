using BlackoutRugby.Api;
using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.Configure<DashboardDefaultsOptions>(builder.Configuration.GetSection("DashboardDefaults"));
builder.Services.Configure<DeveloperOptions>(builder.Configuration.GetSection("Developer"));
builder.Services.AddSingleton<BlackoutRugbyDashboard.Services.SnapshotStore>();
builder.Services.AddScoped<BlackoutRugbyDashboard.Services.ApiLogger>();
builder.Services.AddScoped<BlackoutRugbyDashboard.Services.TeamDashboardService>();
builder.Services.AddSingleton<BlackoutRugbyDashboard.Services.BlackoutRugbyResponseAdapter>();

// Match Cache (spec §Match Cache): one SQLite database at Data/dashboard.db;
// raw XML archived under Data/MatchCache/raw. IBlackoutRugbyApiClient is the
// HTTP-boundary seam — page models and the fill service depend on it, not on
// the concrete client.
builder.Services.AddDbContext<DashboardDbContext>(options =>
    options.UseSqlite($"Data Source={Path.Combine(builder.Environment.ContentRootPath, "Data", "dashboard.db")}"));
builder.Services.AddSingleton<MatchCacheRawStore>(_ =>
    new MatchCacheRawStore(Path.Combine(builder.Environment.ContentRootPath, "Data", "MatchCache")));
builder.Services.AddScoped<IBlackoutRugbyApiClient>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var defaults = configuration.GetSection("DashboardDefaults").Get<DashboardDefaultsOptions>() ?? new DashboardDefaultsOptions();
    var developer = configuration.GetSection("Developer").Get<DeveloperOptions>() ?? new DeveloperOptions();
    var credentials = new BlackoutRugbyApiCredentials(defaults.MemberId, defaults.MemberKey)
    {
        DeveloperId = developer.DeveloperId,
        DeveloperKey = developer.DeveloperKey,
        DeveloperIV = developer.DeveloperIV
    };
    return new BlackoutRugbyApiClient(
        string.IsNullOrWhiteSpace(defaults.BaseEndpoint) ? "http://classic-api.blackoutrugby.com" : defaults.BaseEndpoint,
        credentials);
});
builder.Services.AddScoped<MatchCacheService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
