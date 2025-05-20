using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using ProductivAI_Blazor.Models; // This should now find ApiChatMessage
using System.Runtime.CompilerServices;

namespace ProductivAI_Blazor.Services;

// Helper classes for JSON serialization/deserialization (brought back)
public static class JsonSerializerOptionsProvider
{
    public static JsonSerializerOptions Options { get; }
    static JsonSerializerOptionsProvider()
    {
        Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}

public class ApiRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    [JsonPropertyName("messages")]
    public List<ApiChatMessage> Messages { get; set; } = new List<ApiChatMessage>();
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

public class ApiResponseChunk
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("choices")]
    public List<ApiChoice>? Choices { get; set; }
    [JsonPropertyName("error")]
    public ApiError? Error { get; set; }
}

public class ApiChoice
{
    [JsonPropertyName("delta")]
    public ApiDelta? Delta { get; set; }
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
    [JsonPropertyName("message")]
    public ApiMessage? Message { get; set; }
}

public class ApiDelta
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public class ApiError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

public class ApiMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public class OpenRouterService
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<OpenRouterService> _logger;
    private const string OpenRouterApiUrl = "https://openrouter.ai/api/v1/chat/completions";
    private string? _apiKey;
    private const string SiteUrl = "http://localhost"; 
    private const string SiteName = "ProductivAI-Blazor";
    
    // Storage Keys
    private const string UserWorkDescriptionStorageKey = "userWorkDescription_v1";
    private const string UserShortTermFocusStorageKey = "userShortTermFocus_v1";
    private const string UserLongTermGoalsStorageKey = "userLongTermGoals_v1";
    private const string UserOtherContextStorageKey = "userOtherContext_v1";
    private const string UserSortingPreferenceStorageKey = "userSortingPreference_v1";
    private const string SelectedAiModelStorageKey = "selectedAiModel_v1";
    private const string LastUsedChatModelStorageKey = "lastUsedChatModel_v1";
    private const string ChatHistoryStorageKey = "chatHistory";

    public List<OpenRouterModelInfo> AvailableModels { get; } = new List<OpenRouterModelInfo>
    {
        // Based on TemplateProject/index.html model list
        new OpenRouterModelInfo { Id = "mistralai/mistral-7b-instruct:free", Name = "Mistral 7B Instruct (Free)" },
        new OpenRouterModelInfo { Id = "google/gemini-pro", Name = "Gemini Pro" }, // A common one, keeping it
        new OpenRouterModelInfo { Id = "openai/gpt-3.5-turbo", Name = "GPT-3.5 Turbo" },
        new OpenRouterModelInfo { Id = "openai/gpt-4", Name = "GPT-4" },
        new OpenRouterModelInfo { Id = "google/gemini-2.5-flash-preview", Name = "Gemini 2.5 Flash Preview" },
        new OpenRouterModelInfo { Id = "google/gemini-2.0-flash-001", Name = "Gemini 2.0 Flash" },
        new OpenRouterModelInfo { Id = "google/gemini-2.5-pro-exp-03-25:free", Name = "Gemini 2.5 Pro Exp (Free)" },
        new OpenRouterModelInfo { Id = "google/gemini-2.5-flash-preview:thinking", Name = "Gemini 2.5 Flash (thinking)" },
        new OpenRouterModelInfo { Id = "qwen/qwen2.5-vl-32b-instruct", Name = "Qwen 2.5 VL Instruct" },
        new OpenRouterModelInfo { Id = "qwen/qwq-32b", Name = "Qwen QWQ 32B" },
        new OpenRouterModelInfo { Id = "perplexity/pplx-7b-online", Name = "Perplexity 7B Online" },
        new OpenRouterModelInfo { Id = "deepseek/deepseek-r1", Name = "DeepSeek R1" },
        // Anthropic model can be added if a specific ID is known, e.g.:
        // new OpenRouterModelInfo { Id = "anthropic/claude-3-haiku-20240307", Name = "Claude 3 Haiku" }, 
    };

    public OpenRouterService(
    HttpClient httpClient,
    IJSRuntime jsRuntime,
    ILogger<OpenRouterService> logger,
    IConfiguration configuration)
{
    _httpClient = httpClient;
    _jsRuntime = jsRuntime;
    _logger = logger;
    _apiKey = configuration["OpenRouterApiKey"];
    if (string.IsNullOrEmpty(_apiKey))
    {
        _logger.LogWarning("OpenRouter API key is not configured!");
    }
}

    public async Task<string?> GetSelectedModelIdAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", SelectedAiModelStorageKey);
        }
        catch (JSException ex)
        {
            Console.WriteLine($"Error reading selected model from localStorage: {ex.Message}");
            return null;
        }
    }

    public async Task SetSelectedModelIdAsync(string modelId)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", SelectedAiModelStorageKey, modelId);
        }
        catch (JSException ex)
        {
            Console.WriteLine($"Error saving selected model to localStorage: {ex.Message}");
        }
    }

    // User Context Fields
    // Work Description
    public async Task<string?> GetUserWorkDescriptionAsync()
    {
        return await GetStringFromLocalStorageAsync(UserWorkDescriptionStorageKey);
    }
    public async Task SetUserWorkDescriptionAsync(string? value)
    {
        await SetStringToLocalStorageAsync(UserWorkDescriptionStorageKey, value);
    }

    // Short-Term Focus
    public async Task<string?> GetUserShortTermFocusAsync()
    {
        return await GetStringFromLocalStorageAsync(UserShortTermFocusStorageKey);
    }
    public async Task SetUserShortTermFocusAsync(string? value)
    {
        await SetStringToLocalStorageAsync(UserShortTermFocusStorageKey, value);
    }

    // Long-Term Goals
    public async Task<string?> GetUserLongTermGoalsAsync()
    {
        return await GetStringFromLocalStorageAsync(UserLongTermGoalsStorageKey);
    }
    public async Task SetUserLongTermGoalsAsync(string? value)
    {
        await SetStringToLocalStorageAsync(UserLongTermGoalsStorageKey, value);
    }

    // Other Context
    public async Task<string?> GetUserOtherContextAsync()
    {
        return await GetStringFromLocalStorageAsync(UserOtherContextStorageKey);
    }
    public async Task SetUserOtherContextAsync(string? value)
    {
        await SetStringToLocalStorageAsync(UserOtherContextStorageKey, value);
    }

    // Sorting Preference
    public async Task<string?> GetUserSortingPreferenceAsync()
    {
        // Default to "manual" if not set, as per UserContextModel
        var value = await GetStringFromLocalStorageAsync(UserSortingPreferenceStorageKey);
        return string.IsNullOrEmpty(value) ? "manual" : value;
    }
    public async Task SetUserSortingPreferenceAsync(string? value)
    {
        await SetStringToLocalStorageAsync(UserSortingPreferenceStorageKey, value ?? "manual");
    }

    // Last Used Chat Model
    public async Task<string?> GetLastUsedChatModelIdAsync()
    {
        return await GetStringFromLocalStorageAsync(LastUsedChatModelStorageKey);
    }

    public async Task SetLastUsedChatModelIdAsync(string? modelId)
    {
        if (string.IsNullOrEmpty(modelId))
        {
            // If modelId is null or empty, remove the item from localStorage
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", LastUsedChatModelStorageKey);
            }
            catch (JSException ex)
            {
                Console.WriteLine($"Error removing '{LastUsedChatModelStorageKey}' from localStorage: {ex.Message}");
            }
        }
        else
        {
            // Otherwise, set it using the existing helper which handles potential JSExceptions
            await SetStringToLocalStorageAsync(LastUsedChatModelStorageKey, modelId);
        }
    }

    // Chat History
    public async Task<List<ChatMessage>?> GetChatHistoryAsync()
    {
        var json = await GetStringFromLocalStorageAsync(ChatHistoryStorageKey);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<List<ChatMessage>>(json, JsonSerializerOptionsProvider.Options);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error deserializing chat history: {ex.Message}");
            return null;
        }
    }

    public async Task SetChatHistoryAsync(List<ChatMessage> messages)
    {
        if (messages == null) // Should not happen if called correctly, but good for safety
        {
            await ClearChatHistoryStorageAsync(); // Or throw an argument null exception
            return;
        }
        try
        {
            var json = JsonSerializer.Serialize(messages, JsonSerializerOptionsProvider.Options);
            await SetStringToLocalStorageAsync(ChatHistoryStorageKey, json);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error serializing chat history: {ex.Message}");
        }
    }

    public async Task ClearChatHistoryStorageAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", ChatHistoryStorageKey);
        }
        catch (JSException ex)
        {
            Console.WriteLine($"Error removing '{ChatHistoryStorageKey}' from localStorage: {ex.Message}");
        }
    }

    // Helper methods for local storage access
    private async Task<string?> GetStringFromLocalStorageAsync(string key)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (JSException ex)
        {
            Console.WriteLine($"Error reading '{key}' from localStorage: {ex.Message}");
            return null;
        }
    }

    private async Task SetStringToLocalStorageAsync(string key, string? value)
    {
        try
        {
            if (value == null)
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
            }
            else
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
            }
        }
        catch (JSException ex)
        {
            Console.WriteLine($"Error setting '{key}' in localStorage: {ex.Message}");
        }
    }

    public async IAsyncEnumerable<string> StreamChatCompletionAsync(
        List<ApiChatMessage> conversationHistory,
        string apiKey,
        string selectedModel,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(apiKey))
        {   
            _logger.LogWarning("OpenRouter API key is not set for C# streaming.");
            yield break;
        }

        HttpRequestMessage? request = null;
        HttpResponseMessage? response = null;
        Stream? stream = null;
        StreamReader? reader = null;

        ApiRequest? requestBodyForLog = null; // For logging

        try // Setup Block
        {
            var requestBody = new ApiRequest
            {
                Model = selectedModel,
                Messages = conversationHistory,
                Stream = true
            };
            requestBodyForLog = requestBody; // Assign for logging before potential errors

            // Log the request being sent (summary)
            LogChatRequestSummary("StreamChatCompletionAsync", requestBody);

            request = new HttpRequestMessage(HttpMethod.Post, OpenRouterApiUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonSerializerOptionsProvider.Options), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("HTTP-Referer", SiteUrl);
            request.Headers.Add("X-Title", SiteName);

            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("API request failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                LogApiResponseError("StreamChatCompletionAsync", response.StatusCode, errorContent, requestBodyForLog?.Model);
                // Clean up resources acquired before failure, then exit
                response.Dispose(); 
                request.Dispose();
                yield break;
            }

            stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            reader = new StreamReader(stream);
        }
        catch (OperationCanceledException opEx) // Catch for setup phase
        {
            _logger.LogInformation(opEx, "StreamChatCompletionAsync setup was cancelled for model {ModelId}.", requestBodyForLog?.Model ?? selectedModel);
            // Clean up potentially partially acquired resources
            stream?.Dispose(); // stream might be null
            response?.Dispose(); // response might be null
            request?.Dispose(); // request might be null
            yield break;
        }
        catch (Exception ex) // Catch for setup phase
        {
            _logger.LogError(ex, "Error in StreamChatCompletionAsync setup for model {ModelId}. Request: {@RequestSummary}", requestBodyForLog?.Model ?? selectedModel, SummarizeChatRequest(requestBodyForLog));
            // Clean up potentially partially acquired resources
            stream?.Dispose();
            response?.Dispose();
            request?.Dispose();
            yield break;
        }
        
        // If reader is null here, it means setup failed and we should have already yielded break.
        if (reader == null) 
        { 
            // Redundant cleanup & exit if somehow reached, setup catches should handle this.
            stream?.Dispose(); 
            response?.Dispose();
            request?.Dispose();
            yield break; 
        }

        _logger.LogInformation("StreamChatCompletionAsync: Successfully initiated stream for model {ModelId}.", selectedModel);

        // Yielding loop. The outer try is for the finally block to ensure resource cleanup.
        // This outer try does NOT have a corresponding catch block.
        try
        {
            string? line;
            while (!cancellationToken.IsCancellationRequested && (line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                if (line.StartsWith("data: "))
                {
                    var jsonData = line.Substring(6);
                    if (jsonData == "[DONE]")
                    {
                        break;
                    }

                    string? textChunk = null; // Variable to hold the successfully parsed chunk
                    try // Inner try-catch specifically for JSON parsing errors
                    {
                        var parsedData = JsonSerializer.Deserialize<ApiResponseChunk>(jsonData, JsonSerializerOptionsProvider.Options);
                        textChunk = parsedData?.Choices?.FirstOrDefault()?.Delta?.Content;
                        // if (textChunk != null) _logger.LogDebug("Stream chunk received: {Chunk}", textChunk); // Very verbose
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogWarning(jsonEx, "Error parsing JSON chunk, skipping: {Chunk}", jsonData);
                        // textChunk remains null, effectively skipping this bad chunk.
                    }

                    // Yield return is now OUTSIDE the inner try-catch block.
                    // It's within the outer try, which only has a finally.
                    if (!string.IsNullOrEmpty(textChunk))
                    {
                        yield return textChunk; // THE YIELD RETURN (line 405 in original error)
                    }
                }
            }
        }
        // NO CATCH FOR THE OUTER TRY BLOCK THAT CONTAINS THE WHILE LOOP AND YIELD RETURN
        finally
        {
            _logger.LogInformation("StreamChatCompletionAsync: Stream ended or disposed for model {ModelId}.", selectedModel);
            // This finally ensures cleanup for resources successfully acquired and used by the loop.
            reader.Dispose(); 
            stream?.Dispose(); 
            response?.Dispose(); 
            request?.Dispose(); 
        }
    }
    
    // Overload for the old signature if still used elsewhere, though it's better to update callers
    public async Task StreamChatCompletionAsync(
        List<ApiChatMessage> conversationHistory,
        string selectedModel,
        Func<string, Task> onContentChunkReceived,
        Action onStreamEnd,
        Action<Exception> onError)
    {
        // This method now uses the IAsyncEnumerable version.
        // The _apiKey field is used here as the old signature didn't pass it.
        try
        {
            await foreach (var chunk in StreamChatCompletionAsync(conversationHistory, _apiKey, selectedModel))
            {
                await onContentChunkReceived(chunk);
            }
            onStreamEnd();
        }
        catch (Exception ex)
        {   
            onError(ex);
        }
    }

    public bool ShouldUseJsInteropStreaming(string modelId)
    {
        // Add logic if some models should use C# streaming and others JS.
        // For now, assume all will use JS streaming if appInterop.streamOpenRouterChat is the primary path.
        // Or, if you want to use the C# streaming path more, return false here.
        return true; // Defaulting to true to match ChatPanel's current JS interop preference
    }

    public string GetFocusedMainSystemPrompt(
        string userContextJson, 
        string activeTasksJson, 
        string currentDate,
        string projectsJson)
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("You are ProductivAI, a helpful AI assistant integrated into a Blazor application. Your goal is to help the user be productive by managing tasks, projects, and providing useful information.");
        promptBuilder.AppendLine($"Current date: {currentDate}.");

        promptBuilder.AppendLine("\n## User Context:");
        promptBuilder.AppendLine("This is what the user has told you about their work and goals:");
        promptBuilder.AppendLine(userContextJson); 

        promptBuilder.AppendLine("\n## Existing Projects:");
        promptBuilder.AppendLine("Here is a list of the user's current projects. Each project has an 'Id', 'Name', and 'Description'. Do not suggest creating a project if a similar one already exists. You can use project IDs to refer to specific projects if needed.");
        promptBuilder.AppendLine(projectsJson);

        promptBuilder.AppendLine("\n## Active (Uncompleted) Tasks:");
        promptBuilder.AppendLine("Here is a list of the user's current active tasks. Do not suggest creating a task if a similar one already exists. Tasks have properties like 'Id', 'Name', 'Description', 'ProjectId', etc.");
        promptBuilder.AppendLine(activeTasksJson);

        promptBuilder.AppendLine("\n## Interaction Guidelines:");
        promptBuilder.AppendLine("- Be concise but informative.");
        promptBuilder.AppendLine("- If you generate lists, use markdown formatting.");
        promptBuilder.AppendLine("- If you can identify a clear, actionable task from the conversation that isn't already in the active tasks list, you can include `@@CAN_SUGGEST_TASK@@` at the end of your response. The UI will then offer to generate a detailed task suggestion based on your response. Do not use this if suggesting a project.");
        promptBuilder.AppendLine("- If you need a due date for a task being discussed, you can include `@@REQUEST_DUE_DATE@@` at the end of your response. The UI will then show a date picker.");

        promptBuilder.AppendLine("\n## Project Creation Suggestions:");
        promptBuilder.AppendLine("- Analyze the user's request. If it seems like they are describing a new initiative, a larger goal, or a collection of related complex tasks that would benefit from being grouped into a new project, you can suggest creating one.");
        promptBuilder.AppendLine("- BEFORE suggesting a new project, ALWAYS check the provided 'Existing Projects' list (projectsJson) and 'Active (Uncompleted) Tasks' list (activeTasksJson) to ensure a similar project or task doesn't already exist.");
        promptBuilder.AppendLine("- If you determine a new project is warranted and no similar one exists, use the following specific format ON ITS OWN LINE at the end of your response (after any other text you want to provide the user):");
        promptBuilder.AppendLine("  `[AI_SUGGEST_PROJECT name=\"Suggested Project Name\" description=\"Brief description for the project (max 2-3 sentences).\"]`");
        promptBuilder.AppendLine("- Replace `\"Suggested Project Name\"` with a concise and relevant name for the project based on the user's request.");
        promptBuilder.AppendLine("- Replace `\"Brief description for the project (max 2-3 sentences).\"` with a short description. Keep it brief.");
        promptBuilder.AppendLine("- ONLY use this format. Do not try to make up other tags or ways to suggest projects.");
        promptBuilder.AppendLine("- If you use the `[AI_SUGGEST_PROJECT ...]` tag, do NOT use `@@CAN_SUGGEST_TASK@@` or `@@REQUEST_DUE_DATE@@` in the same response.");
        promptBuilder.AppendLine("- After the user creates a project via the UI (or a project context is otherwise established), your next steps for that project are:");
        promptBuilder.AppendLine("  1. Acknowledge the current project focus (e.g., \"Okay, we're now focused on Project X.\").");
        promptBuilder.AppendLine("  2. Engage in a conversational brainstorming session to elicit multiple 'Task Ideas' from the user for this project. These are lightweight starting points (e.g., just a name or a brief concept).");
        promptBuilder.AppendLine("  3. After discussing several ideas, summarize them and propose adding them to the project in bulk using the following specific tag format ON ITS OWN LINE:");
        promptBuilder.AppendLine("     `[AI_SUGGEST_TASK_IDEAS project_id=\"{ID_OF_CURRENT_PROJECT}\" ideas=[\"Idea Name 1\", \"Another Idea Name\"]]`");
        promptBuilder.AppendLine("     (Replace {ID_OF_CURRENT_PROJECT} with the actual ID of the project being discussed. The `ideas` attribute should be a valid JSON string array of the task idea names discussed.)");
        promptBuilder.AppendLine("- When using `[AI_SUGGEST_TASK_IDEAS ...]`, do not use other markers like `@@CAN_SUGGEST_TASK@@`.");
        promptBuilder.AppendLine("- The user will then be able to review, modify, and approve these ideas in the chat UI before they are saved.");

        promptBuilder.AppendLine("\n## Quick Reply Options:");
        promptBuilder.AppendLine("- You can suggest up to 3 concise quick reply options for the user. If you do, format them like this at the very end of your response, each on a new line, enclosed in markers: `@@OPTIONS_START@@\nOption 1 text\nOption 2 text\nOption 3 text\n@@OPTIONS_END@@`");
        promptBuilder.AppendLine("- Quick replies should be very short, like button labels.");

        promptBuilder.AppendLine("\nPlease respond to the user's latest message based on these guidelines and the provided context.");

        return promptBuilder.ToString();
    }

    public async Task<List<string>> GetQuickReplyOptionsAsync(
        string precedingAiMessageContent,
        List<ProductivAI_Blazor.Models.ChatMessage> conversationHistoryUi, 
        ProductivAI_Blazor.Models.UserContextModel? currentUserContext, 
        string apiKey,
        string selectedModelId)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("OpenRouter API key is not set for GetQuickReplyOptionsAsync.");
            return new List<string>();
        }

        string userContextJson = "{}";
        if (currentUserContext != null)
        {
            try
            {
                userContextJson = JsonSerializer.Serialize(new 
                { 
                    WorkDescription = currentUserContext.WorkDescription,
                    ShortTermFocus = currentUserContext.ShortTermFocus,
                    LongTermGoals = currentUserContext.LongTermGoals
                }, JsonSerializerOptionsProvider.Options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serializing UserContextModel for quick reply options.");
            }
        }
        
        string conversationHistoryForPromptJson = "[]";
        if (conversationHistoryUi != null && conversationHistoryUi.Any())
        {
            try
            {
                var recentHistoryForPrompt = conversationHistoryUi
                    .Select(uiMsg => new ApiChatMessage(uiMsg.Role, uiMsg.Content ?? string.Empty))
                    .ToList(); // Using the full UI history passed, ChatPanel controls how much is passed
                conversationHistoryForPromptJson = JsonSerializer.Serialize(recentHistoryForPrompt, JsonSerializerOptionsProvider.Options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serializing conversation history for quick reply options prompt.");
            }
        }

        // Constructing the system prompt for generating quick reply options
        string systemPromptForOptions = $"""
            You are an AI assistant helping a user with productivity. The user has just received the following message from you (the AI):
            ---
            AI's PRECEDING MESSAGE:
            {precedingAiMessageContent}
            ---
            The user might also provide some context about their current work or goals:
            ---
            USER CONTEXT (if available):
            Work Description: {currentUserContext?.WorkDescription ?? "Not provided"}
            Short-Term Focus: {currentUserContext?.ShortTermFocus ?? "Not provided"}
            Long-Term Goals: {currentUserContext?.LongTermGoals ?? "Not provided"}
            Other Relevant Context: {currentUserContext?.OtherContext ?? "Not provided"}
            ---
            Based on the AI's PRECEDING MESSAGE and the user's context, generate 2-4 concise, distinct quick reply options that the user might want to send as their *next* message.
            These options should be from the user's perspective.
            Focus on actionable next steps or common responses.

            CRITICAL INSTRUCTIONS FOR OPTION GENERATION:
            1.  ONLY generate options if the AI's PRECEDING MESSAGE genuinely invites a choice, asks a question that needs a varied response, or presents information where follow-up actions are natural next steps for the user.
            2.  DO NOT generate options if the AI's PRECEDING MESSAGE is a simple statement, an acknowledgement, a status update that doesn't require user input, or if the AI is primarily confirming information provided by the user (like a due date).
            3.  SPECIFICALLY, if the AI's PRECEDING MESSAGE is about confirming, setting, or discussing a due date (e.g., contains "due date", "deadline", or a phrase like "Okay, I've set the due date to..."), DO NOT generate any options. In such cases, output the special token: @@NO_OPTIONS@@
            4.  If the AI's message asks a question, the options should be plausible answers.
            5.  If the AI's message is a statement (and options are appropriate as per rule 1), the options could be acknowledgments, follow-up questions, or related actions.
            6.  Ensure options are genuinely helpful and not redundant.

            IMPORTANT (Handling Naming Requests): If the AI's PRECEDING MESSAGE is primarily asking the user to provide a "name" for something (e.g., a task, a project),
            your options should focus on:
            1. Suggesting an example name (e.g., "Call it 'Project Alpha'").
            2. Asking for more time to think of a name (e.g., "I'll name it later").
            3. Stating they don't want to name it (e.g., "No name needed for now").
            4. Proposing a generic name (e.g., "Use a generic name for now").
            DO NOT suggest options like "Okay" or "Sounds good" in this specific "naming" scenario.

            If you determine that NO options should be generated based on the CRITICAL INSTRUCTIONS (especially rule 2 and 3), output ONLY the exact token @@NO_OPTIONS@@ on a single line and nothing else.
            Otherwise, provide ONLY the options, each on a new line. Do not include any other text, preamble, or numbering.
            Example (if options are appropriate):
            Option 1
            Option 2
            Option 3
            """;

        var optionsMessages = new List<ApiChatMessage>
        {
            new ApiChatMessage("system", systemPromptForOptions),
            new ApiChatMessage("user", $"The primary AI's last message was: \n\n\"{precedingAiMessageContent}\"\n\nBased on this and the provided conversation history/user context, suggest suitable quick reply options for the user.") 
        };

        var requestBody = new ApiRequest
        {
            Model = selectedModelId,
            Messages = optionsMessages,
            Stream = false
        };

        LogChatRequestSummary("GetQuickReplyOptionsAsync", requestBody);

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, OpenRouterApiUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonSerializerOptionsProvider.Options), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("HTTP-Referer", SiteUrl); 
            request.Headers.Add("X-Title", SiteName);

            _logger.LogInformation("Sending request to OpenRouter for quick reply options. Model: {ModelId}", selectedModelId);
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error from OpenRouter API for quick replies (Status {StatusCode}): {ErrorContent}", response.StatusCode, errorContent);
                return new List<string>(); // Return empty list on error
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Raw response for quick replies: {ResponseContent}", responseContent);

            string actualAiOutput;
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(responseContent))
                {
                    actualAiOutput = doc.RootElement
                                        .GetProperty("choices")[0]
                                        .GetProperty("message")
                                        .GetProperty("content")
                                        .GetString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing JSON response for quick reply options. Response content: {ResponseContent}", responseContent);
                return new List<string>();
            }
            
            _logger.LogInformation("Extracted AI output for quick replies: {ActualAiOutput}", actualAiOutput);

            // Check for the special @@NO_OPTIONS@@ token
            if (actualAiOutput.Trim() == "@@NO_OPTIONS@@")
            {
                _logger.LogInformation("AI indicated no options should be generated (@@NO_OPTIONS@@ received in content).");
                return new List<string>();
            }

            // Assuming the AI returns a list of options, each on a new line.
            List<string> options = new List<string>();
            
            if (string.IsNullOrWhiteSpace(actualAiOutput))
            {
                _logger.LogWarning("Quick reply options content (extracted AI output) was empty. Original response: {FullResponse}", responseContent);
                return options;
            }

            // The prompt specifies options are newline-separated.
            // Sanitization for ```json might not be necessary if the AI follows the "each on a new line" instruction.
            // However, if AI might still wrap its plain text in markdown code blocks:
            string sanitizedAiOutput = actualAiOutput.Trim();
            if (sanitizedAiOutput.StartsWith("```") && sanitizedAiOutput.EndsWith("```"))
            {
                sanitizedAiOutput = sanitizedAiOutput.Substring(3, sanitizedAiOutput.Length - 6).Trim();
            }
            // Remove specific "```json" or "```text" if they appear
            if (sanitizedAiOutput.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                sanitizedAiOutput = sanitizedAiOutput.Substring(7).TrimStart();
            }
            else if (sanitizedAiOutput.StartsWith("```text", StringComparison.OrdinalIgnoreCase))
            {
                sanitizedAiOutput = sanitizedAiOutput.Substring(7).TrimStart();
            }
            else if (sanitizedAiOutput.StartsWith("```")) // General case after specific ones
            {
                 sanitizedAiOutput = sanitizedAiOutput.Substring(3).TrimStart();
                 // No need to check for trailing ``` here as it's handled by the initial block check or might not exist
            }


            if (string.IsNullOrWhiteSpace(sanitizedAiOutput))
            {
                _logger.LogWarning("Quick reply options content was empty after sanitization. Original AI output: {OriginalContent}", actualAiOutput);
                return options;
            }

            options = sanitizedAiOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(o => o.Trim())
                                     .Where(o => !string.IsNullOrEmpty(o))
                                     .ToList();
            
            _logger.LogInformation("Successfully parsed {OptionCount} quick reply options.", options.Count);
            return options;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetQuickReplyOptionsAsync for model {ModelId}. Request: {@RequestSummary}", selectedModelId, SummarizeChatRequest(requestBody));
            return new List<string>();
        }
    }

    public async Task<AiTaskSuggestion?> GetTaskSuggestionFromChatAsync(
        List<ApiChatMessage> conversationHistory, // Use ApiChatMessage
        string userContextJson,
        string projectsJson,
        string apiKey,
        string selectedModelId)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("OpenRouter API key is not set for GetTaskSuggestionFromChatAsync.");
            return null;
        }

        string systemPromptForTaskSuggestion = @"You are a task extraction assistant. Your role is to analyze a provided conversation history, with a special focus on the messages leading up to a `@@TASK_READY@@` signal from another AI.
Based on this conversation, your goal is to generate a structured JSON object representing a single task.

The conversation history, user context (if available), and a list of existing projects (if available) will be provided.

**Your Task:**

1.  **Identify the Core Task:** From the conversation, determine the primary task that was defined.
2.  **Extract Key Information:**
    *   `Name`: A concise name for the task. This is mandatory.
    *   `AiContext`: A brief explanation or background for the task, derived from the conversation. This helps remember why the task was created.
    *   `DueDate`: If a due date was discussed or confirmed, provide it in ""YYYY-MM-DD"" format. If not clearly defined, this can be null.
    *   `Importance`: An integer from 1 (low) to 5 (high). Infer this from the conversation if possible; otherwise, default to 3.
    *   `ContextDetails`: Any additional notes, requirements, or specific details about the task.
    *   `Subtasks`: An array of subtask objects, where each subtask has:
        *   `Name`: Name of the subtask. This is mandatory for each subtask.
        *   `IsCompleted`: Defaults to `false`.
        *   `DueDate`: (Optional) ""YYYY-MM-DD"" format.
        *   `Importance`: (Optional) Integer 1-5, defaults to 3 if not specified.
        *   `ContextDetails`: (Optional) Additional notes for the subtask.
        If no subtasks were clearly defined, provide an empty array `[]`.
    *   `ProjectId`: If the conversation indicates the task should be associated with an existing project (from the provided projects list), include the `Id` of that project. If no project association is clear, this can be null.

**Input Provided to You:**

*   **Conversation History (JSON):** Recent messages between the user and the primary AI. The last AI message in this history will contain `@@TASK_READY@@`.
*   **User Context (JSON, optional):** General context about the user: {userContextJson}
*   **Projects (JSON array, optional):** A list of existing projects: {projectsJson}

**Output Format:**

*   Respond ONLY with a single, valid JSON object matching the structure of the `AiTaskSuggestion` model described above.
*   Do NOT include any other text, explanations, or markdown formatting outside the JSON object.
*   If you cannot confidently extract a task or essential fields like 'Name' are missing from the conversation, return an empty JSON object `{}`.

Example Output:
```json
{
  ""Name"": ""Draft initial mockups"",
  ""AiContext"": ""Task to create initial design mockups for the new website, based on user's request after finalizing the project scope."",
  ""DueDate"": ""2023-12-15"",
  ""Importance"": 4,
  ""ContextDetails"": ""Focus on homepage and product page. User wants a modern and clean design."",
  ""Subtasks"": [
    { ""Name"": ""Research competitor websites"", ""IsCompleted"": false, ""DueDate"": ""2023-12-10"", ""Importance"": 3, ""ContextDetails"": ""Look at 3-4 top competitors."" },
    { ""Name"": ""Sketch wireframes"", ""IsCompleted"": false, ""DueDate"": ""2023-12-12"", ""Importance"": 4, ""ContextDetails"": """" }
  ],
  ""ProjectId"": ""proj1""
}
```
";

        // Apply replacements on the fully defined string
        systemPromptForTaskSuggestion = systemPromptForTaskSuggestion
                                           .Replace("{userContextJson}", userContextJson)
                                           .Replace("{projectsJson}", projectsJson);

        var messagesForApi = new List<ApiChatMessage>
        {
            new ApiChatMessage("system", systemPromptForTaskSuggestion) // Use constructor
        };
        
        // Create a user message that encapsulates the instruction and the history
        // This is a common pattern: give system prompt, then user prompt restates goal + provides data.
        string userPromptContent = $@"Based on the following conversation history:
```json
{JsonSerializer.Serialize(conversationHistory, JsonSerializerOptionsProvider.Options)}
```
Please generate the task suggestion JSON as instructed in the system prompt.";

        messagesForApi.Add(new ApiChatMessage("user", userPromptContent)); // Use constructor


        var requestBody = new ApiRequest
        {
            Model = selectedModelId,
            Messages = messagesForApi,
            Stream = false 
        };

        LogChatRequestSummary("GetTaskSuggestionFromChatAsync", requestBody);

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, OpenRouterApiUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonSerializerOptionsProvider.Options), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("HTTP-Referer", SiteUrl); 
            request.Headers.Add("X-Title", SiteName);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("GetTaskSuggestionFromChatAsync API call successful for model {ModelId}. Response Body: {ResponseBody}", selectedModelId, responseBody);

                var jsonResponse = JsonDocument.Parse(responseBody);
                var contentElement = jsonResponse.RootElement
                                .GetProperty("choices")[0]
                                .GetProperty("message")
                                .GetProperty("content");
                
                string? content = contentElement.ValueKind == JsonValueKind.String ? contentElement.GetString() : null;
                
                if (!string.IsNullOrEmpty(content))
                {
                    string sanitizedContent = content.Trim();
                    if (sanitizedContent.StartsWith("```json"))
                    {
                        sanitizedContent = sanitizedContent.Substring(7);
                        if (sanitizedContent.EndsWith("```"))
                        {
                            sanitizedContent = sanitizedContent.Substring(0, sanitizedContent.Length - 3);
                        }
                    }
                    else if (sanitizedContent.StartsWith("```")) 
                    {
                         sanitizedContent = sanitizedContent.Substring(3);
                         if (sanitizedContent.EndsWith("```"))
                         {
                            sanitizedContent = sanitizedContent.Substring(0, sanitizedContent.Length - 3);
                         }
                    }
                    sanitizedContent = sanitizedContent.Trim();

                    if (string.IsNullOrWhiteSpace(sanitizedContent) || sanitizedContent == "{{}}")
                    {
                        _logger.LogWarning("Task suggestion content was empty or just '{{}}'. Original content: {Content}", content);
                        return null;
                    }
                    
                    try
                    {
                        var taskSuggestion = JsonSerializer.Deserialize<ProductivAI_Blazor.Models.AiTaskSuggestion>(sanitizedContent, JsonSerializerOptionsProvider.Options);
                        
                        if (taskSuggestion != null && !string.IsNullOrWhiteSpace(taskSuggestion.Name))
                        {
                            return taskSuggestion;
                        }
                        else
                        {
                             _logger.LogWarning("Deserialized task suggestion is null or has no name. Sanitized content: {Content}", sanitizedContent);
                            return null;
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "Failed to deserialize task suggestion JSON content. Sanitized content was: {Content}", sanitizedContent);
                        return null;
                    }
                }
                _logger.LogWarning("Task suggestion content was null or empty from API response. Full response: {FullResponse}", responseBody);
                return null;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                LogApiResponseError("GetTaskSuggestionFromChatAsync", response.StatusCode, errorContent, requestBody.Model);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetTaskSuggestionFromChatAsync for model {ModelId}. Request: {@RequestSummary}", selectedModelId, SummarizeChatRequest(requestBody));
            return null;
        }
    }

    public async Task<string?> GetAiEnhancedTaskDevelopmentAsync(
        List<ApiChatMessage> conversationHistory,
        TaskItemModel originalIdea,
        string userContextJson,
        string apiKey,
        string selectedModelId)
    {
        _logger.LogInformation($"[OpenRouterService] GetAiEnhancedTaskDevelopmentAsync called for idea: {originalIdea.Name}");

        var systemPromptBuilder = new StringBuilder();
        systemPromptBuilder.AppendLine("You are an expert project assistant. Your task is to help the user develop a task idea into a more complete task definition based on a previous discussion and the original idea details.");
        systemPromptBuilder.AppendLine("You will be provided with the original task idea (JSON format) and the recent conversation history.");
        systemPromptBuilder.AppendLine("Review all provided information carefully.");
        systemPromptBuilder.AppendLine("Your goal is to enhance the original idea. This might involve:");
        systemPromptBuilder.AppendLine("- Refining the task name for clarity or actionability.");
        systemPromptBuilder.AppendLine("- Generating a detailed description or context for the task.");
        systemPromptBuilder.AppendLine("- Suggesting a list of specific, actionable subtasks if the discussion implies them. For EACH subtask, you MUST provide an 'Importance' value (0-100).");
        systemPromptBuilder.AppendLine("- Estimating an appropriate importance level (0-100). This 'Importance' field for the main task is MANDATORY.");
        systemPromptBuilder.AppendLine("- Suggesting a due date if the context provides clues (format: YYYY-MM-DDTHH:MM:SSZ).");
        systemPromptBuilder.AppendLine("- Ensure the 'IsIdea' property is set to false.");
        systemPromptBuilder.AppendLine("- Maintain the original ProjectId if provided.");
        systemPromptBuilder.AppendLine("You MUST return ONLY a single, valid JSON object representing the enhanced task. Do NOT include any other text, explanations, or conversational pleasantries before or after the JSON object.");
        systemPromptBuilder.AppendLine("The JSON object should conform to the following C# TaskItemModel structure (subtasks are optional but include the array if any are generated):");
        systemPromptBuilder.AppendLine("```json");
        systemPromptBuilder.AppendLine("{");
        systemPromptBuilder.AppendLine("  \"Id\": 0, // Or the original ID if you are only updating fields");
        systemPromptBuilder.AppendLine("  \"Name\": \"Enhanced Task Name\",");
        systemPromptBuilder.AppendLine("  \"ContextDetails\": \"Detailed description generated by AI based on discussion.\",");
        systemPromptBuilder.AppendLine("  \"Importance\": 75, // MANDATORY for the main task");
        systemPromptBuilder.AppendLine("  \"ProjectId\": " + (originalIdea.ProjectId?.ToString() ?? "null") + ",");
        systemPromptBuilder.AppendLine("  \"DueDate\": null, // Or \"YYYY-MM-DDTHH:MM:SSZ\"");
        systemPromptBuilder.AppendLine("  \"IsCompleted\": false,");
        systemPromptBuilder.AppendLine("  \"IsIdea\": false,");
        systemPromptBuilder.AppendLine("  \"AiContext\": \"Relevant AI discussion summary or keywords\", // Optional");
        systemPromptBuilder.AppendLine("  \"Subtasks\": [");
        systemPromptBuilder.AppendLine("    { \"Id\": 0, \"Name\": \"Suggested Subtask 1\", \"IsCompleted\": false, \"TaskId\": 0, \"DueDate\": null, \"Importance\": 50, \"Context\": null }, // 'Importance' is MANDATORY for each subtask");
        systemPromptBuilder.AppendLine("    { \"Id\": 0, \"Name\": \"Suggested Subtask 2\", \"IsCompleted\": false, \"TaskId\": 0, \"DueDate\": null, \"Importance\": 60, \"Context\": null }  // 'Importance' is MANDATORY for each subtask");
        systemPromptBuilder.AppendLine("  ]");
        systemPromptBuilder.AppendLine("}");
        systemPromptBuilder.AppendLine("```");
        systemPromptBuilder.AppendLine("If no subtasks are generated, return an empty array for Subtasks: `\"Subtasks\": []`.");
        systemPromptBuilder.AppendLine("If the original idea has an ID, you can choose to include it or use 0 for a new representation.");

        var messagesForApi = new List<ApiChatMessage>
        {
            new ApiChatMessage("system", systemPromptBuilder.ToString()),
            new ApiChatMessage("user", $"Original Task Idea:\n```json\n{JsonSerializer.Serialize(originalIdea, JsonSerializerOptionsProvider.Options)}\n```\n\nConversation History:\n{string.Join("\n", conversationHistory.Select(m => $"{m.Role}: {m.Content}"))}\n\nPlease provide the enhanced task JSON object.")
        };

        var requestBody = new ApiRequest
        {
            Model = selectedModelId,
            Messages = messagesForApi,
            Stream = false // We need a single JSON response, not a stream
        };

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, OpenRouterApiUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonSerializerOptionsProvider.Options), Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            request.Headers.TryAddWithoutValidation("HTTP-Referer", SiteUrl); 
            request.Headers.TryAddWithoutValidation("X-Title", SiteName); 

            LogChatRequestSummary(nameof(GetAiEnhancedTaskDevelopmentAsync), requestBody);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"[OpenRouterService] GetAiEnhancedTaskDevelopmentAsync raw response: {responseContent}");
                var apiResponse = JsonSerializer.Deserialize<ApiResponseChunk>(responseContent, JsonSerializerOptionsProvider.Options);
                // Corrected access path for non-streaming content
                var aiMessageContent = apiResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? apiResponse?.Choices?.FirstOrDefault()?.Delta?.Content;

                if (!string.IsNullOrWhiteSpace(aiMessageContent))
                {
                    // The AI might still wrap the JSON in backticks or add minor text. Try to extract pure JSON.
                    var jsonStartIndex = aiMessageContent.IndexOf('{');
                    var jsonEndIndex = aiMessageContent.LastIndexOf('}');
                    if (jsonStartIndex != -1 && jsonEndIndex != -1 && jsonEndIndex > jsonStartIndex)
                    {
                        var extractedJson = aiMessageContent.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
                        _logger.LogInformation($"[OpenRouterService] GetAiEnhancedTaskDevelopmentAsync extracted JSON: {extractedJson}");
                        return extractedJson;
                    }
                    else
                    {
                        _logger.LogWarning($"[OpenRouterService] GetAiEnhancedTaskDevelopmentAsync: Could not find valid JSON object in AI response: {aiMessageContent}");
                        return null;
                    }
                }
                _logger.LogWarning($"[OpenRouterService] GetAiEnhancedTaskDevelopmentAsync: AI response content was empty or not in expected structure. Full response: {responseContent}");
                return null;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                LogApiResponseError(nameof(GetAiEnhancedTaskDevelopmentAsync), response.StatusCode, errorContent, selectedModelId);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenRouterService] Exception in GetAiEnhancedTaskDevelopmentAsync");
            return null;
        }
    }

    // Method to load the full UserContextModel
    public async Task<UserContextModel> LoadUserContextAsync()
    {
        var context = new UserContextModel
        {
            WorkDescription = await GetUserWorkDescriptionAsync(),
            ShortTermFocus = await GetUserShortTermFocusAsync(),
            LongTermGoals = await GetUserLongTermGoalsAsync(),
            OtherContext = await GetUserOtherContextAsync(),
            SortingPreference = await GetUserSortingPreferenceAsync(),
            SelectedAiModel = await GetSelectedModelIdAsync(),
        };
        context.Projects = new List<ProjectModel>(); // Initialize to empty list, will be populated by ProjectService elsewhere
        return context;
    }
    
    // Method to get UserContext as JSON string for prompts
    public async Task<string> GetUserContextJsonAsync()
    {
        var userContext = await LoadUserContextAsync();
        if (userContext == null) return "{}";

        try
        {
            // Serialize only the parts relevant for the AI prompt
            return JsonSerializer.Serialize(new 
            { 
                userContext.WorkDescription,
                userContext.ShortTermFocus,
                userContext.LongTermGoals,
                userContext.OtherContext
                // Do not include SelectedAiModel or SortingPreference unless the prompt needs them
            }, JsonSerializerOptionsProvider.Options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serializing UserContextModel to JSON.");
            return "{}";
        }
    }

    // Helper methods for logging summaries
    private void LogChatRequestSummary(string methodName, ApiRequest requestBody)
    {
        if (requestBody == null) return;
        var summary = SummarizeChatRequest(requestBody);
        _logger.LogInformation("{MethodName}: Sending API request to model {ModelId}. Messages: {MessageCount}. Last User Message: '{LastUserMessage}'. System Prompt Exists: {HasSystemPrompt}", 
            methodName, summary.ModelId, summary.MessageCount, summary.LastUserMessage, summary.HasSystemPrompt);
    }

    private ChatRequestSummary SummarizeChatRequest(ApiRequest? requestBody)
    {
        if (requestBody == null) return new ChatRequestSummary { ModelId = "N/A" };

        var lastUserMsg = requestBody.Messages?.LastOrDefault(m => m.Role == "user")?.Content ?? "N/A";
        if (lastUserMsg.Length > 100) lastUserMsg = lastUserMsg.Substring(0, 97) + "...";
        
        return new ChatRequestSummary 
        {
            ModelId = requestBody.Model,
            MessageCount = requestBody.Messages?.Count ?? 0,
            LastUserMessage = lastUserMsg,
            HasSystemPrompt = requestBody.Messages?.Any(m => m.Role == "system") ?? false
        };
    }

    private void LogApiResponseError(string methodName, System.Net.HttpStatusCode statusCode, string errorContent, string? modelId = null)
    {
        _logger.LogError("{MethodName}: API request failed for model {ModelId}. Status: {StatusCode}. Error: {ErrorContent}", 
            methodName, modelId ?? "N/A", statusCode, errorContent);
    }

    private struct ChatRequestSummary
    {
        public string ModelId { get; set; }
        public int MessageCount { get; set; }
        public string LastUserMessage { get; set; }
        public bool HasSystemPrompt { get; set; }
    }
} 