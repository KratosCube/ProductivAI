using ProductivAI_Blazor.Models; // Assuming your models are here
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;

namespace ProductivAI_Blazor.Services;

public class TaskService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public TaskService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            // ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve, // Removed/Commented out
            // Consider DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull if appropriate for your DTOs
        };
    }

    public async Task<List<TaskItemModel>?> GetTasksAsync(bool includeCompleted = false)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/tasks?includeCompleted={includeCompleted}");
            response.EnsureSuccessStatusCode(); // Throws an exception if not successful
            return await response.Content.ReadFromJsonAsync<List<TaskItemModel>>(_jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error fetching tasks: {ex.Message}");
            // Potentially throw a custom exception or return a default/empty list
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while fetching tasks: {ex.Message}");
            return null;
        }
    }

    public async Task<TaskItemModel?> GetTaskByIdAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/tasks/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TaskItemModel>(_jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error fetching task {id}: {ex.Message}");
            return null;
        }
         catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while fetching task {id}: {ex.Message}");
            return null;
        }
    }

    public async Task<TaskItemModel?> CreateTaskAsync(TaskItemModel task)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/tasks", task, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TaskItemModel>(_jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error creating task: {ex.Message}");
            // Consider returning the error content: await response.Content.ReadAsStringAsync();
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while creating task: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateTaskAsync(int id, TaskItemModel task)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/tasks/{id}", task, _jsonOptions);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error updating task {id}: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        { 
            Console.WriteLine($"An unexpected error occurred while updating task {id}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteTaskAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/tasks/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error deleting task {id}: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while deleting task {id}: {ex.Message}");
            return false;
        }
    }
} 