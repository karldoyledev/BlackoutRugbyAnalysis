using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Routes & gates (D2): default-deny via AuthorizeFolder; Login + SignUp are the
// only anonymous pages; authenticated users hitting them bounce to Home in the
// page models. No logout route — sign-out is a Settings POST handler.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/SignUp");
});
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.Configure<DashboardDefaultsOptions>(builder.Configuration.GetSection("DashboardDefaults"));
builder.Services.Configure<DeveloperOptions>(builder.Configuration.GetSection("Developer"));
builder.Services.AddSingleton<BlackoutRugbyDashboard.Services.SnapshotStore>();
builder.Services.AddScoped<BlackoutRugbyDashboard.Services.ApiLogger>();
builder.Services.AddSingleton<BlackoutRugbyDashboard.Services.BlackoutRugbyResponseAdapter>();

// Accounts & Club Link (D3): one DashboardDbContext hosts the Identity sets and
// the Match Cache; relaxed policy (8+ alphanumeric), lockout on (5 → 5 min), no
// email confirmation — a single-operator local tool. Registered via AddIdentityCore
// + the application cookie scheme (no default Identity UI — these pages are ours).
builder.Services.AddIdentityCore<DashboardUser>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireDigit = false;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<DashboardDbContext>()
.AddSignInManager();

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

// Remember-me keeps the ticket at the full 14 days (sliding); without it the
// ticket is capped at ~12 h — never renewed, since the sliding threshold (half
// of 14 days) sits far beyond that cap.
builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Events.OnSigningIn = context =>
    {
        var now = DateTimeOffset.UtcNow;
        context.Properties.IssuedUtc ??= now;
        context.Properties.ExpiresUtc = context.Properties.IsPersistent
            ? now + TimeSpan.FromDays(14)
            : now + TimeSpan.FromHours(12);
        return Task.CompletedTask;
    };
});

// Match Cache (spec §Match Cache): one SQLite database at Data/dashboard.db;
// raw XML archived under Data/MatchCache/raw. IBlackoutRugbyApiClient is the
// HTTP-boundary seam — page models and the fill service depend on it, not on
// the concrete client.
builder.Services.AddDbContext<DashboardDbContext>(options =>
    options.UseSqlite($"Data Source={Path.Combine(builder.Environment.ContentRootPath, "Data", "dashboard.db")}"));
builder.Services.AddSingleton<MatchCacheRawStore>(_ =>
    new MatchCacheRawStore(Path.Combine(builder.Environment.ContentRootPath, "Data", "MatchCache")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IBlackoutRugbyApiClientFactory, BlackoutRugbyApiClientFactory>();
builder.Services.AddScoped<IMemberKeyProtector, MemberKeyProtector>();
builder.Services.AddScoped<ClubLinkService>();
builder.Services.AddScoped<MatchCacheService>();

// The per-request default client (D3): developer credentials plus the signed-in
// User's linked member credentials — developer-only when anonymous or unlinked.
// Member credentials never come from configuration again (secrets scrub).
builder.Services.AddScoped<IBlackoutRugbyApiClient>(sp =>
{
    var factory = sp.GetRequiredService<IBlackoutRugbyApiClientFactory>();
    var clubLinks = sp.GetRequiredService<ClubLinkService>();
    var credentials = clubLinks.ResolveCurrentMemberCredentials();
    return credentials is null
        ? factory.CreateDeveloperOnly()
        : factory.CreateForMember(credentials.MemberId, credentials.MemberKey);
});

var app = builder.Build();

// Migrations live in the web project (D3); a local tool (dotnet-ef in the
// manifest) authored them, and startup applies whatever is pending.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<DashboardDbContext>().Database.Migrate();
}

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

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();

