using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using ProductivAI_Blazor.Services; // Assuming this is the namespace for OpenRouterService
using ProductivAI_Blazor; // Added to bring App.razor's namespace into scope
using Microsoft.Extensions.Configuration; // Added for AddJsonStream

public class Program // Added class Program wrapper
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        // --- START: Load custom Blazor configuration for API Key ---
        var httpClientForConfig = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
        try
        {
            var response = await httpClientForConfig.GetAsync("appsettings.Blazor.json");
            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync();
                builder.Configuration.AddJsonStream(stream);
                Console.WriteLine("[INFO] appsettings.Blazor.json loaded successfully.");
            }
            else
            {
                Console.WriteLine($"[ERROR] Failed to load appsettings.Blazor.json. Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Exception while loading appsettings.Blazor.json: {ex.Message}");
        }
        // --- END: Load custom Blazor configuration ---

        // Register OpenRouterService and ensure IJSRuntime is available for it
        builder.Services.AddScoped<OpenRouterService>();

        // Register TaskService
        builder.Services.AddSingleton<TaskService>();

        // Register ProjectService
        builder.Services.AddScoped<ProjectService>();

        // Register TaskModalService as Singleton
        builder.Services.AddSingleton<TaskModalService>();

        // Register NavigationStateService as Singleton
        builder.Services.AddSingleton<NavigationStateService>();

        // Register HttpClient for API calls, using configured BLAZOR_API_BASE_URL
        builder.Services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var apiBaseUrlString = config["BLAZOR_API_BASE_URL"];
            if (string.IsNullOrEmpty(apiBaseUrlString))
            {
                Console.WriteLine("[ERROR] BLAZOR_API_BASE_URL is not configured. Using HostEnvironment.BaseAddress as fallback for API.");
                apiBaseUrlString = builder.HostEnvironment.BaseAddress;
            }
            else
            {
                Console.WriteLine($"[API HttpClient] Base address configured from BLAZOR_API_BASE_URL: {apiBaseUrlString}");
            }
            return new HttpClient { BaseAddress = new Uri(apiBaseUrlString) };
        });

        await builder.Build().RunAsync();
    }
} 