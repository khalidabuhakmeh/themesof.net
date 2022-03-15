using Terrajobst.GitHubEvents;
using Terrajobst.GitHubEvents.AspNetCore;

using ThemesOfDotNet.Services;

using Toolbelt.Blazor.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddApplicationInsightsTelemetry(builder.Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"]);
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();
builder.Services.AddHostedService(p => p.GetRequiredService<GitHubEventProcessingService>());
builder.Services.AddHostedService(p => p.GetRequiredService<AzureDevOpsCrawlerService>());
builder.Services.AddHostedService(p => p.GetRequiredService<OspoCrawlerService>());
builder.Services.AddSingleton<WorkspaceService>();
builder.Services.AddSingleton<ValidationService>();
builder.Services.AddScoped<QueryContextProvider>();
builder.Services.AddScoped<WorkItemAccessCheckService>();
builder.Services.AddGitHubAuth(builder.Configuration);
builder.Services.AddSingleton<GitHubEventProcessingService>();
builder.Services.AddSingleton<IGitHubEventProcessor>(p => p.GetRequiredService<GitHubEventProcessingService>());
builder.Services.AddSingleton<AzureDevOpsCrawlerService>();
builder.Services.AddSingleton<OspoCrawlerService>();
builder.Services.AddSingleton<ProductTeamService>();
builder.Services.AddHotKeys();

var gitHubWebHookSecret = builder.Configuration["GitHubWebHookSecret"];

var app = builder.Build();

// Intialize workspace service

var workspaceService = app.Services.GetRequiredService<WorkspaceService>();
await workspaceService.InitializeAsync();

// Validate

var validationService = app.Services.GetRequiredService<ValidationService>();
validationService.Initialize();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub();
app.MapDefaultControllerRoute();
app.MapGitHubWebHook(secret: gitHubWebHookSecret);
app.MapFallbackToPage("/_Host");

app.Run();
