using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Shared.Resilience;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();

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

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}

// Use CORS
app.UseCors("AllowAngularApp");

// Use authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Use YARP
app.MapReverseProxy();

// Map health checks
app.MapHealthChecks("/health");

// Add a simple endpoint to test the gateway
app.MapGet("/", () => "ERP Inventory Module API Gateway - Running")
   .WithName("GatewayStatus")
   .WithTags("Gateway");

app.Run();
