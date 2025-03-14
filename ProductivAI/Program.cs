using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ProductivAI;
using ProductivAI.Core.Interfaces;
using ProductivAI.Application.Services;
using ProductivAI.Infrastructure.Repositories;
using ProductivAI.AIServices;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
    Timeout = TimeSpan.FromSeconds(30)
});

// Register repositories
builder.Services.AddScoped<INoteRepository, LocalStorageNoteRepository>();
builder.Services.AddScoped<ITaskRepository, LocalStorageTaskRepository>();

// Register services
builder.Services.AddScoped<INoteService, NoteService>();
builder.Services.AddScoped<ITaskService, TaskService>();

// Register the default AI service with the IAIService interface
builder.Services.AddScoped<IAIService>(sp =>
    new DefaultAIService(
        sp.GetRequiredService<HttpClient>(),
        "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
        "qwen32b"
    )
);

// Always use a real OpenRouterAIService, but with a dummy API key
// This will fall back to simulated responses when the API key is invalid
builder.Services.AddScoped<OpenRouterAIService>(sp =>
    new OpenRouterAIService(
        sp.GetRequiredService<HttpClient>(),
        "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", // This will trigger simulation mode in the service
        sp.GetRequiredService<IJSRuntime>(),
        "qwen32b",
        "ProductivAI"
    )
);

await builder.Build().RunAsync();