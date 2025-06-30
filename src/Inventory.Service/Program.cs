using Inventory.Service.Data;
using Inventory.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Shared.Resilience;
using Shared.Logging;
using Shared.Observability;
using Serilog;

// Configure Serilog early in the application startup
Log.Logger = LoggingConfiguration.CreateLogger("Inventory.Service");

var builder = WebApplication.CreateBuilder(args);

// Use Serilog
builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configure OpenTelemetry with shared configuration
builder.Services.AddObservability(
    serviceName: "Inventory.Service",
    serviceVersion: "1.0.0",
    environment: builder.Environment.EnvironmentName,
    configureTracing: tracing => tracing
        .AddSource("Inventory.Service"),
    configureMetrics: metrics => metrics
        .AddMeter("Inventory.Service"));

// Add authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtConfig = builder.Configuration.GetSection("JWT");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtConfig["Issuer"],
            ValidAudience = jwtConfig["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig["Key"]!)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Add Entity Framework with Oracle
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseOracle(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add application services
builder.Services.AddScoped<IInventoryService, Inventory.Service.Services.InventoryService>();
builder.Services.AddSingleton<IAlertProducerService, AlertProducerService>();

// Add resilience policies
builder.Services.AddResiliencePolicies();

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<InventoryDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}

// Add logging middleware
app.UseRequestResponseLogging();

// Add observability middleware
app.UseObservabilityMiddleware();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map health checks
app.MapHealthChecks("/health");

// Map metrics endpoint for Prometheus
app.MapPrometheusScrapingEndpoint("/metrics");

// Add a simple endpoint to test the service
app.MapGet("/", () => "Inventory Service - Running")
   .WithName("InventoryServiceStatus")
   .WithTags("Status");

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await DbInitializer.InitializeAsync(context);
}

try
{
    Log.Information("Starting Inventory Service");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Inventory Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible for testing
public partial class Program { }
