var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();

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

// Use YARP
app.MapReverseProxy();

// Map health checks
app.MapHealthChecks("/health");

// Add a simple endpoint to test the gateway
app.MapGet("/", () => "ERP Inventory Module API Gateway - Running")
   .WithName("GatewayStatus")
   .WithTags("Gateway");

app.Run();
