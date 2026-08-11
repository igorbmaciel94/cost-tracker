using CostTracker.Api.Configuration;
using CostTracker.Api.Middleware;
using CostTracker.Api.Services;
using CostTracker.Application.Integrations.Ai;
using CostTracker.Application.Investments.MarketData;
using CostTracker.Application.Options;
using CostTracker.Application.Pdf;
using CostTracker.Application.Projections;
using CostTracker.Application.Services;
using CostTracker.Infrastructure.Extensions;
using CostTracker.Infrastructure.Investments.MarketData;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using QuestPDF.Infrastructure;
using System.Threading.RateLimiting;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<AuthOptions>()
    .Bind(builder.Configuration.GetSection("Auth"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Username), "Auth:Username is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.PasswordHash), "Auth:PasswordHash is required.")
    .ValidateOnStart();

builder.Services.AddControllers(options =>
{
    options.Filters.Add(new AuthorizeFilter());
});
builder.Services.AddOpenApi();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "costtracker.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsProduction()
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddOptions<AnthropicOptions>()
    .Bind(builder.Configuration.GetSection("Anthropic"))
    .ValidateOnStart();

builder.Services.AddSingleton<IAiAnalysisClient, ClaudeAnalysisClient>();
builder.Services.AddSingleton<IPdfRenderer, AnalysisPdfRenderer>();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInvestmentMarketData(builder.Configuration);
builder.Services.AddScoped<PortfolioProjectionService>();
builder.Services.AddSingleton<InvestmentMarketDataRefreshSignal>();
builder.Services.AddSingleton<IMarketDataRefreshScheduler>(provider =>
    provider.GetRequiredService<InvestmentMarketDataRefreshSignal>());
builder.Services.AddScoped<PortfolioManagementService>();
builder.Services.AddScoped<InvestmentMarketDataService>();
builder.Services.AddScoped<ContributionPlanningService>();
builder.Services.AddHostedService<InvestmentMarketDataRefreshWorker>();
builder.Services.AddScoped<MonthProjectionService>();
builder.Services.AddScoped<PasswordHashService>();
builder.Services.AddScoped<MonthService>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<EntryService>();
builder.Services.AddScoped<TargetsService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<PlanningService>();
builder.Services.AddScoped<FinancialHealthService>();
builder.Services.AddScoped<AiAnalysisService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("frontend");
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.Services.InitializeDatabaseAsync();
await app.RunAsync();

public partial class Program;
