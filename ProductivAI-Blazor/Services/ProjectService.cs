using ProductivAI_Blazor.Models; // Assuming your models are here, we'll need a ProjectModel
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;

namespace ProductivAI_Blazor.Services;

// We'll need a ProjectModel, let's define a simple one here for now 
// or assume it exists in ProductivAI_Blazor.Models
// For now, let's assume a simple ProjectModel exists or we will create it.
// If ProductivAI_Blazor.Models.ProjectModel does not exist, it will need to be created.
// For example: public class ProjectModel { public int Id { get; set; } public string Name { get; set; } ... }

public class ProjectService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ProjectService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            // ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve // Removed/Commented out
        };
    }

    public async Task<List<ProjectModel>?> GetProjectsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/projects");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ProjectModel>>(_jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error fetching projects: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while fetching projects: {ex.Message}");
            return null;
        }
    }

    public async Task<ProjectModel?> GetProjectByIdAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/projects/{id}");
             if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProjectModel>(_jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error fetching project {id}: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while fetching project {id}: {ex.Message}");
            return null;
        }
    }

    public async Task<ProjectModel?> CreateProjectAsync(ProjectModel project)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/projects", project, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProjectModel>(_jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error creating project: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while creating project: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateProjectAsync(int id, ProjectModel project)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/projects/{id}", project, _jsonOptions);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error updating project {id}: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while updating project {id}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteProjectAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/projects/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error deleting project {id}: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while deleting project {id}: {ex.Message}");
            return false;
        }
    }
} 