using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Xwing.Web.Components;
using Xwing.Web.Data;
using Xwing.Web.Stores;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<XwingDbContext>("xwing");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddScoped<ITournamentStore, TournamentStore>();

var app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    XwingDbContext db = scope.ServiceProvider.GetRequiredService<XwingDbContext>();
    await db.Database.MigrateAsync().ConfigureAwait(false);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

await app.RunAsync();
