using Microsoft.EntityFrameworkCore;
using ProductivAI.Server.Data;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// 1. Configure DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine($"[DEBUG] Retrieved ConnectionString 'DefaultConnection': {connectionString}"); // Diagnostic line
if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("[ERROR] DefaultConnection string is null or empty.");
    // Optionally, you could throw an exception here or handle it to prevent app startup without a DB connection
}
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure JSON options to handle reference cycles
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// 2. Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorApp", policyBuilder =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Allow any origin, header, and method during development for easier testing from other devices
            policyBuilder.AllowAnyOrigin() 
                         .AllowAnyHeader()
                         .AllowAnyMethod();
                        // .AllowCredentials(); // Only include AllowCredentials if your frontend sends credentials AND you trust all origins you might test from.
                                             // For broader AllowAnyOrigin, it's often safer to omit AllowCredentials unless specifically needed.
        }
        else
        {
            // For production, use specific origins from configuration
            var allowedProdOrigins = builder.Configuration.GetValue<string>("CorsOrigins")?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            if (allowedProdOrigins.Any())
            {
                policyBuilder.WithOrigins(allowedProdOrigins)
                             .AllowAnyHeader()
                             .AllowAnyMethod();
                // Consider .AllowCredentials() for production only if absolutely necessary and origins are highly specific.
            }
            else
            {
                // Fallback or error if no production origins are configured - this is a safety measure.
                // Or, you might have a default secure policy.
                // For now, if no origins are specified for prod, it won't allow cross-origin requests.
                // The LogWarning will be handled after 'app' is built and logger is available.
            }
        }
    });
});

// OpenAPI/Swagger services removed as per user request
// builder.Services.AddEndpointsApiExplorer(); 
// builder.Services.AddSwaggerGen(); 

var app = builder.Build();

// Get a logger instance after the app is built
var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Check and log CORS warning for production if necessary
if (!app.Environment.IsDevelopment())
{
    var allowedProdOrigins = app.Configuration.GetValue<string>("CorsOrigins")?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
    if (!allowedProdOrigins.Any())
    {
        logger.LogWarning("CORS: No production origins configured. Cross-origin requests might be blocked in production if not handled by other means.");
    }
}

// Apply EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        // Ensure the database is created and migrations are applied.
        // Use dbContext.Database.EnsureCreated() if you are not using migrations or want to ensure DB exists first.
        // Use dbContext.Database.Migrate() to apply pending migrations.
        dbContext.Database.Migrate(); 
        Console.WriteLine("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        // Use the logger for errors as well
        logger.LogError(ex, "An error occurred while migrating the database.");
        // Optionally, rethrow or handle as appropriate for your application's startup.
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // OpenAPI/Swagger UI removed as per user request
    // app.UseSwagger(); 
    // app.UseSwaggerUI(); 
}

// 3. Use CORS middleware - place it before UseRouting, UseAuthentication, UseAuthorization, and MapControllers
app.UseCors("AllowBlazorApp");

// HTTPS Redirection (if you re-enable HTTPS later)
// app.UseHttpsRedirection();

app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
