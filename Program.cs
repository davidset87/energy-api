using EnergyApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSingleton(TimeProvider.System);

var carbonApiBaseUrl = builder.Configuration["CarbonIntensityApi:BaseUrl"]
                       ?? "https://api.carbonintensity.org.uk/";

builder.Services.AddHttpClient<IEnergyService, EnergyService>(client =>
{
    client.BaseAddress = new Uri(carbonApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var allowedOrigin = builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:5173";
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigin)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// No HTTPS redirection: the container only listens on HTTP and TLS is terminated by the host (Render).
app.UseCors("AllowFrontend");
app.MapControllers();

app.Run();