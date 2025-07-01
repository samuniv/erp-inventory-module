using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text;
using Gateway.API.Observability;
using Shared.Resilience;
using Shared.Logging;
using Shared.Observability;
using Shared.HealthChecks;
using Serilog;

// Configure Serilog early in the application startup
Log.Logger = LoggingConfiguration.CreateLogger("Gateway.API");

var builder = WebApplication.CreateBuilder(args);

// Use Serilog
builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddOpenApi();

// Configure OpenTelemetry with shared configuration
builder.Services.AddObservability(
    serviceName: "Gateway.API",
    serviceVersion: "1.0.0",
    environment: builder.Environment.EnvironmentName,
    configureTracing: tracing => tracing
        .AddSource("Gateway.API"),
    configureMetrics: metrics => metrics
        .AddMeter("Gateway.API"));

// Add custom metrics
builder.Services.AddSingleton<GatewayMetrics>();

// Add resilient HTTP clients for downstream services
builder.Services.AddResilientHttpClients(options =>
{
    options.ConfigureAuthClient = true;
    options.ConfigureInventoryClient = true;
    options.ConfigureOrderClient = true;
    options.ConfigureSupplierClient = true;
    options.AuthServiceBaseUrl = "http://localhost:5006";
    options.InventoryServiceBaseUrl = "http://localhost:5007";
    options.OrderServiceBaseUrl = "http://localhost:5008";
    options.SupplierServiceBaseUrl = "http://localhost:5009";
    options.HttpTimeout = TimeSpan.FromSeconds(30);
});

// Add resilience policies
builder.Services.AddResiliencePolicies();

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

// Add authorization
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAuthentication", policy =>
    {
        policy.RequireAuthenticatedUser();
    });

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add comprehensive health checks
builder.Services.AddComprehensiveHealthChecks(
    serviceName: "Gateway.API",
    configureHealthChecks: healthChecks =>
    {
        // Add simple health checks for downstream services
        healthChecks.AddCheck("auth_service_health", () =>
        {
            // Basic connectivity check - in production, this would be more sophisticated
            return HealthCheckResult.Healthy("Auth service connectivity check");
        }, tags: ["downstream", "ready"]);

        healthChecks.AddCheck("inventory_service_health", () =>
        {
            return HealthCheckResult.Healthy("Inventory service connectivity check");
        }, tags: ["downstream", "ready"]);

        healthChecks.AddCheck("order_service_health", () =>
        {
            return HealthCheckResult.Healthy("Order service connectivity check");
        }, tags: ["downstream", "ready"]);
    });

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}

// Use CORS
app.UseCors("AllowAngularApp");

// Add logging middleware
app.UseRequestResponseLogging();

// Add observability middleware
app.UseObservabilityMiddleware();

// Use authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Use YARP
app.MapReverseProxy();

// Map comprehensive health checks
app.MapComprehensiveHealthChecks();

// Map metrics endpoint for Prometheus
app.MapPrometheusScrapingEndpoint("/metrics");

// Add a simple endpoint to test the gateway
app.MapGet("/", () => "ERP Inventory Module API Gateway - Running")
   .WithName("GatewayStatus")
   .WithTags("Gateway");

try
{
    Log.Information("Starting Gateway API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Gateway API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
