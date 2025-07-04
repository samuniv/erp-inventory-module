using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Order.Service.Data;
using Order.Service.Services;
using Order.Service.Events;
using Order.Service.Observability;
using Shared.Logging;
using Shared.Observability;
using Shared.HealthChecks;
using System.Text;
using Serilog;
using OpenTelemetry.Instrumentation.EntityFrameworkCore;

// Configure Serilog early in the application startup
Log.Logger = LoggingConfiguration.CreateLogger("Order.Service");

var builder = WebApplication.CreateBuilder(args);

// Use Serilog
builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

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

// Configure OpenTelemetry with shared configuration
builder.Services.AddObservability(
    serviceName: "Order.Service",
    serviceVersion: "1.0.0",
    environment: builder.Environment.EnvironmentName,
    configureTracing: tracing => tracing
        .AddSource("Order.Service"),
    configureMetrics: metrics => metrics
        .AddMeter("Order.Service"));

// Add custom metrics
builder.Services.AddSingleton<OrderMetrics>();

// Add Entity Framework with PostgreSQL
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Kafka options
builder.Services.Configure<KafkaOptions>(
    builder.Configuration.GetSection(KafkaOptions.SectionName));

// Add business services
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
builder.Services.AddScoped<IInventoryAlertHandler, EnhancedInventoryAlertHandler>();

// Add hosted services
builder.Services.AddHostedService<InventoryAlertConsumerService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add comprehensive health checks
builder.Services.AddComprehensiveHealthChecks(
    serviceName: "Order.Service",
    configureHealthChecks: healthChecks =>
    {
        healthChecks.AddDbContextCheck<OrderDbContext>(tags: ["database", "ready", "live"]);

        // Add Kafka health check if available
        // healthChecks.AddKafka(options => { /* configure */ }, tags: ["kafka", "ready"]);
    });

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

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map comprehensive health checks
app.MapComprehensiveHealthChecks();

// Map metrics endpoint for Prometheus
app.MapPrometheusScrapingEndpoint("/metrics");

// Add a simple endpoint to test the service
app.MapGet("/", () => "Order Service - Running")
   .WithName("OrderServiceStatus")
   .WithTags("Status");

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await DbInitializer.InitializeAsync(context);
}

try
{
    Log.Information("Starting Order Service");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Order Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible for testing
public partial class Program { }
