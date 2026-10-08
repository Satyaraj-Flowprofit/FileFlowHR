using FileFlowHR.Application;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Infrastructure;
using FileFlowHR.Infrastructure.Email;
using FileFlowHR.Web.Components;
using FileFlowHR.Web.Components.Account;
using FileFlowHR.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// No SQL Server on this laptop? Set FILEFLOWHR_DB=Sqlite to use a local file database instead.
if (string.Equals(Environment.GetEnvironmentVariable("FILEFLOWHR_DB"), "Sqlite", StringComparison.OrdinalIgnoreCase))
    builder.Configuration.AddJsonFile("appsettings.Sqlite.json", optional: false);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Layers: use cases (Application) and their implementations (Infrastructure).
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Sign-in: cookies for FileFlowHR accounts, plus one scheme per company login system (added at runtime).
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "FileFlowHR.Auth";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Permissions: one policy per permission code, checked against the user's role claims.
builder.Services.AddAuthorization(options => PermissionPolicies.Register(options));
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPrincipalAccessor, PrincipalAccessor>();

if (builder.Environment.IsDevelopment())
    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapAdditionalIdentityEndpoints();

if (app.Environment.IsDevelopment())
{
    // Local only: emails are shown here instead of being sent.
    app.MapGet("/dev/mailbox.json", (DevMailbox mailbox) => mailbox.Messages);
}

await app.InitialiseFileFlowHRAsync();
app.Run();

public partial class Program;
