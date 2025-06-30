using Auth.Service.Data;
using Auth.Service.Models;
using Auth.Service.Services;
using Auth.Service.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Shared.Resilience;
using Shared.Logging;
using Shared.Observability;
using Serilog;

// Configure Serilog early in the application startup
Log.Logger = LoggingConfiguration.CreateLogger("Auth.Service");

var builder = WebApplication.CreateBuilder(args);

// Use Serilog
builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddOpenApi();

// Configure OpenTelemetry with shared configuration
builder.Services.AddObservability(
    serviceName: "Auth.Service",
    serviceVersion: "1.0.0",
    environment: builder.Environment.EnvironmentName,
    configureTracing: tracing => tracing
        .AddSource("Auth.Service"),
    configureMetrics: metrics => metrics
        .AddMeter("Auth.Service"));

// Add database context
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 6;

    // User settings
    options.User.RequireUniqueEmail = true;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AuthDbContext>()
.AddDefaultTokenProviders();

// Add JWT authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"]!)),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Add authorization
builder.Services.AddAuthorization();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add custom services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, Auth.Service.Services.AuthService>();

// Add resilience policies
builder.Services.AddResiliencePolicies();

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AuthDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}

// Use CORS
app.UseCors("AllowAll");

// Add logging middleware
app.UseRequestResponseLogging();

// Add observability middleware
app.UseObservabilityMiddleware();

// Use authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    await DbInitializer.InitializeAsync(context, userManager, roleManager);
}

// Map endpoints
app.MapPost("/api/auth/register", async (RegisterDto registerDto, IAuthService authService) =>
{
    try
    {
        var result = await authService.RegisterAsync(registerDto);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
})
.WithName("Register")
.WithTags("Authentication");

app.MapPost("/api/auth/login", async (LoginDto loginDto, IAuthService authService) =>
{
    try
    {
        var result = await authService.LoginAsync(loginDto);
        return Results.Ok(result);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
})
.WithName("Login")
.WithTags("Authentication");

app.MapPost("/api/auth/refresh", async (RefreshTokenDto refreshTokenDto, IAuthService authService) =>
{
    try
    {
        var result = await authService.RefreshTokenAsync(refreshTokenDto);
        return Results.Ok(result);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
})
.WithName("RefreshToken")
.WithTags("Authentication");

app.MapPost("/api/auth/forgot-password", async (ForgotPasswordDto forgotPasswordDto, IAuthService authService) =>
{
    try
    {
        var result = await authService.ForgotPasswordAsync(forgotPasswordDto);
        return Results.Ok(new { message = "If the email exists, a reset link has been sent." });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
})
.WithName("ForgotPassword")
.WithTags("Authentication");

app.MapPost("/api/auth/reset-password", async (ResetPasswordDto resetPasswordDto, IAuthService authService) =>
{
    try
    {
        var result = await authService.ResetPasswordAsync(resetPasswordDto);
        if (result)
        {
            return Results.Ok(new { message = "Password reset successfully." });
        }
        return Results.BadRequest(new { error = "Failed to reset password." });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
})
.WithName("ResetPassword")
.WithTags("Authentication");

app.MapPost("/api/auth/revoke", async (string refreshToken, IAuthService authService) =>
{
    try
    {
        var result = await authService.RevokeTokenAsync(refreshToken);
        if (result)
        {
            return Results.Ok(new { message = "Token revoked successfully." });
        }
        return Results.BadRequest(new { error = "Failed to revoke token." });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
})
.WithName("RevokeToken")
.WithTags("Authentication")
.RequireAuthorization();

// Health check endpoint
app.MapHealthChecks("/health");

// Map metrics endpoint for Prometheus
app.MapPrometheusScrapingEndpoint("/metrics");

// Status endpoint
app.MapGet("/", () => "Auth Service - Running")
   .WithName("AuthServiceStatus")
   .WithTags("Status");

try
{
    Log.Information("Starting Auth Service");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Auth Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
