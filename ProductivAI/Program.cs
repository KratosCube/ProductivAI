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

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register repositories
builder.Services.AddScoped<INoteRepository, LocalStorageNoteRepository>();
builder.Services.AddScoped<ITaskRepository, LocalStorageTaskRepository>();

// Register services
builder.Services.AddScoped<INoteService, NoteService>();
builder.Services.AddScoped<ITaskService, TaskService>();

// Register AI service - for initial testing with simulated responses
builder.Services.AddScoped<IAIService>(sp =>
    new DefaultAIService(
        sp.GetRequiredService<HttpClient>(),
        "your-api-key-here",
        "Son35"
    )
);

// Also register the OpenRouter version for the chat page
builder.Services.AddScoped<OpenRouterAIService>(sp =>
    new OpenRouterAIService(
        sp.GetRequiredService<HttpClient>(),
        "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",  // Replace with your key
        sp.GetRequiredService<IJSRuntime>(),
        "Son35",
        "ProductivAI"
    )
);

await builder.Build().RunAsync();