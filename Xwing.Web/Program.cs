using Aspire.Microsoft.EntityFrameworkCore.SqlServer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Xwing.Web.Auth;
using Xwing.Web.Components;
using Xwing.Web.Data;
using Xwing.Web.Stores;
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddDbContextFactory<XwingDbContext>((serviceProvider, options) =>
{
    IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
    string? connectionString = configuration.GetConnectionString("xwing");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Connection string 'xwing' not found.");
    }
    options.UseSqlServer(connectionString, sqlOptions => sqlOptions.EnableRetryOnFailure());
});
builder.EnrichSqlServerDbContext<XwingDbContext>(
    configureSettings: settings => settings.DisableRetry = true);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(Xwing.Web.Auth.AuthCookieSettings.PersistentCookieDays);
        options.LoginPath = "/";
        options.AccessDeniedPath = "/";
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole(UserRoles.Admin));
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, HttpContextAuthenticationStateProvider>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<StoreConcurrencyHelpers>();
builder.Services.AddSingleton<PasswordHasher<User>>();
builder.Services.AddScoped<IUserStore, UserStore>();
builder.Services.AddScoped<ITournamentStore, TournamentStore>();
var app = builder.Build();
using (IServiceScope scope = app.Services.CreateScope())
{
    IDbContextFactory<XwingDbContext> factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<XwingDbContext>>();
    await using XwingDbContext db = await factory.CreateDbContextAsync().ConfigureAwait(false);
    await db.Database.MigrateAsync().ConfigureAwait(false);
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();
await app.RunAsync();
