using Microsoft.JSInterop;
using ProductivAI.Core;
using ProductivAI.Core.Interfaces;
using ProductivAI.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProductivAI.AIServices
{


    public class OpenRouterAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _modelName;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly string _appName;
        private readonly IJSRuntime _jsRuntime;

        // OpenRouter-specific constants
        private const string ApiBaseUrl = "https://openrouter.ai/api/v1";
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 1000;

        public OpenRouterAIService(HttpClient httpClient, string apiKey, IJSRuntime jsRuntime, string modelName = "Son35", string appName = "ProductivAI")
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _modelName = modelName ?? "Son35";
            _appName = appName ?? "ProductivAI";

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            // Configure HttpClient for OpenRouter
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }

            // OpenRouter-specific headers
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://productivai.app");
            _httpClient.DefaultRequestHeaders.Add("X-Title", _appName);
        }

        public string GetModelName() => _modelName;
        public async Task ProcessQueryWithStreamingAsync(
    string query,
    UserContext context,
    StreamingResponseCallback callback,
    CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                callback("I couldn't understand your query. Could you please provide more details?", true);
                return;
            }

            try
            {
                // Construct system prompt with user context
                var systemPrompt = ConstructSystemPromptFromContext(context);

                // Prepare request for AI model with streaming enabled
                var requestBody = new
                {
                    model = GetModelIdentifier(),
                    messages = new[]
                    {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = query }
            },
                    max_tokens = 1000,
                    temperature = 0.7,
                    stream = true,
                    include_reasoning = context.UseReasoning
                };

                // Create HTTP request message
                var jsonContent = JsonSerializer.Serialize(requestBody, _jsonOptions);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/chat/completions")
                {
                    Content = content
                };

                // Send request and process streaming response
                var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();

                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (line.StartsWith("data: "))
                    {
                        var data = line.Substring(6);

                        // Check for "[DONE]" message
                        if (data == "[DONE]")
                        {
                            callback("", true);
                            break;
                        }

                        try
                        {
                            using var jsonDoc = JsonDocument.Parse(data);
                            var root = jsonDoc.RootElement;

                            if (root.TryGetProperty("choices", out var choices) &&
                                choices.ValueKind == JsonValueKind.Array &&
                                choices.GetArrayLength() > 0)
                            {
                                var choice = choices[0];

                                if (choice.TryGetProperty("delta", out var delta) &&
                                    delta.TryGetProperty("content", out var content_token))
                                {
                                    var token = content_token.GetString();
                                    if (!string.IsNullOrEmpty(token))
                                    {
                                        callback(token, false);
                                    }
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // Skip malformed JSON
                            continue;
                        }
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    callback("\n[Generation stopped]", true);
                }
            }
            catch (Exception ex)
            {
                callback($"\nError: {ex.Message}", true);
            }
        }
        public async Task<string> ProcessQueryAsync(string query, UserContext context)
        {
            if (string.IsNullOrWhiteSpace(query))
                return "I couldn't understand your query. Could you please provide more details?";

            try
            {
                // Construct system prompt with user context
                var systemPrompt = ConstructSystemPromptFromContext(context);

                // Prepare request for AI model
                var requestBody = new
                {
                    model = GetModelIdentifier(),
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = query }
                    },
                    max_tokens = 1000,
                    temperature = 0.7,
                    stream = false // Not streaming in this method
                };

                // Call AI model API
                var response = await CallAIModelAsync(requestBody);

                // Parse and return response
                return ParseAIResponse(response) ?? "I couldn't process your query at this time.";
            }
            catch (Exception ex)
            {
                return $"I encountered an issue while processing your query: {ex.Message}";
            }
        }

        // Implementation of IAIService.PrioritizeTasksAsync
        public async Task<List<TaskItem>> PrioritizeTasksAsync(List<TaskItem> tasks, UserContext context)
        {
            if (tasks == null || !tasks.Any())
                return new List<TaskItem>();

            try
            {
                // Create a task representation for the AI
                var taskRepresentations = tasks.Select(t => new
                {
                    id = t.Id.ToString(),
                    title = t.Title,
                    description = t.Description,
                    dueDate = t.DueDate,
                    priority = t.Priority,
                    subTaskCount = t.SubTasks?.Count ?? 0,
                    subTasksCompleted = t.SubTasks?.Count(st => st.IsCompleted) ?? 0
                }).ToList();

                // Create context representation
                var contextRepresentation = new
                {
                    workDescription = context.WorkDescription,
                    focusAreas = context.FocusAreas,
                    longTermGoals = context.LongTermGoals
                };

                // System prompt for prioritization
                var systemPrompt = "You are a productivity assistant that helps prioritize tasks. " +
                    "Analyze the provided tasks in relation to the user's context, and return a JSON array of task IDs " +
                    "in order of recommended priority (most important first). Consider deadlines, importance, alignment with goals, and dependencies.";

                // Request body
                var requestBody = new
                {
                    model = GetModelIdentifier(),
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = JsonSerializer.Serialize(new { tasks = taskRepresentations, context = contextRepresentation }, _jsonOptions) }
                    },
                    response_format = new { type = "json_object" },
                    max_tokens = 1000,
                    temperature = 0.3,
                    stream = false
                };

                // Call AI model API
                var response = await CallAIModelAsync(requestBody);

                // Parse the prioritized task ID list
                var prioritizedTaskIds = ParsePrioritizedTaskIds(response);
                if (prioritizedTaskIds == null || !prioritizedTaskIds.Any())
                    return tasks;

                // Re-order tasks based on the prioritized IDs
                var orderedTasks = new List<TaskItem>();
                foreach (var id in prioritizedTaskIds)
                {
                    var task = tasks.FirstOrDefault(t => t.Id.ToString() == id);
                    if (task != null)
                    {
                        orderedTasks.Add(task);
                    }
                }

                // Add any tasks that weren't included in the prioritized list
                var remainingTasks = tasks.Where(t => !orderedTasks.Contains(t)).ToList();
                orderedTasks.AddRange(remainingTasks);

                return orderedTasks;
            }
            catch (Exception)
            {
                // In case of error, return the original task list
                return tasks;
            }
        }

        // Implementation of IAIService.GenerateTaskSuggestionsAsync
        public async Task<string> GenerateTaskSuggestionsAsync(UserContext context)
        {
            if (context == null)
                return "I need more information about your work and goals to suggest tasks.";

            try
            {
                // System prompt for task suggestions
                var systemPrompt = "You are a productivity assistant that helps generate task suggestions. " +
                    "Based on the user's context, generate a list of 3-5 suggested tasks that would help them " +
                    "make progress on their goals. Each suggestion should be specific, actionable, and relevant.";

                // Prepare request
                var requestBody = new
                {
                    model = GetModelIdentifier(),
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = $"Work: {context.WorkDescription}\nFocus Areas: {string.Join(", ", context.FocusAreas)}\nLong-term Goals: {string.Join(", ", context.LongTermGoals)}" }
                    },
                    max_tokens = 1000,
                    temperature = 0.7,
                    stream = false
                };

                // Call AI model API
                var response = await CallAIModelAsync(requestBody);

                // Parse and return suggestions
                return ParseAIResponse(response) ?? "I couldn't generate task suggestions at this time.";
            }
            catch (Exception ex)
            {
                return $"I encountered an issue while generating task suggestions: {ex.Message}";
            }
        }

        // Implementation of IAIService.EnhanceNoteAsync
        public async Task<string> EnhanceNoteAsync(string noteContent, UserContext context)
        {
            if (string.IsNullOrWhiteSpace(noteContent))
                return noteContent;

            try
            {
                // System prompt for note enhancement
                var systemPrompt = "You are a productivity assistant that helps enhance notes. " +
                    "Improve the provided note content by organizing information, highlighting key points, " +
                    "and adding relevant context or connections based on the user's work and interests. " +
                    "Preserve all original information while making it more useful and actionable.";

                // Prepare contextual information
                var contextInfo = string.IsNullOrEmpty(context?.WorkDescription) ?
                    string.Empty :
                    $"\nContext: {context.WorkDescription}";

                if (context?.FocusAreas?.Any() == true)
                {
                    contextInfo += $"\nFocus Areas: {string.Join(", ", context.FocusAreas)}";
                }

                // Prepare request
                var requestBody = new
                {
                    model = GetModelIdentifier(),
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = $"Note content: {noteContent}{contextInfo}" }
                    },
                    max_tokens = Math.Max(2000, noteContent.Length * 2),
                    temperature = 0.4,
                    stream = false
                };

                // Call AI model API
                var response = await CallAIModelAsync(requestBody);

                // Parse and return enhanced note
                var enhancedNote = ParseAIResponse(response);
                return string.IsNullOrWhiteSpace(enhancedNote) ? noteContent : enhancedNote;
            }
            catch (Exception)
            {
                // In case of any error, return the original note content
                return noteContent;
            }
        }

        // Implementation of IAIService.ParseCommandAsync
        public async Task<string> ParseCommandAsync(string command, UserContext context)
        {
            if (string.IsNullOrWhiteSpace(command))
                return "I couldn't understand your command.";

            try
            {
                // System prompt for command parsing
                var systemPrompt = "You are a productivity assistant that helps parse user commands. " +
                    "Interpret the user's command and generate a structured response that specifies: " +
                    "1. The command type (e.g., create_task, add_note, search, etc.) " +
                    "2. The specific parameters of the command (e.g., title, due date, etc.) " +
                    "3. Any additional context or clarifications needed.";

                // Prepare request
                var requestBody = new
                {
                    model = GetModelIdentifier(),
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = command }
                    },
                    response_format = new { type = "json_object" },
                    max_tokens = 500,
                    temperature = 0.2,
                    stream = false
                };

                // Call AI model API
                var response = await CallAIModelAsync(requestBody);

                // Parse the command parsing result
                return ParseCommandResult(response) ?? "I couldn't parse your command.";
            }
            catch (Exception ex)
            {
                return $"I encountered an issue while parsing your command: {ex.Message}";
            }
        }

        // NEW: Streaming method for chat interactions
        public async Task ProcessQueryWithStreamingAsync(string query, UserContext context, List<MessageHistory> conversationHistory, StreamingResponseCallback callback, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                callback("I couldn't understand your query. Could you please provide more details?", true);
                return;
            }

            try
            {
                // Construct system prompt with user context
                var systemPrompt = ConstructSystemPromptFromContext(context);

                // Build messages array including conversation history
                var messagesArray = new List<object>();

                // First add the system message
                messagesArray.Add(new { role = "system", content = systemPrompt });

                // Then add all previous messages from history
                foreach (var message in conversationHistory)
                {
                    messagesArray.Add(new
                    {
                        role = message.IsUserMessage ? "user" : "assistant",
                        content = message.Content
                    });
                }

                // Finally add the current user query
                messagesArray.Add(new { role = "user", content = query });

                // Prepare request for AI model with streaming enabled
                var requestBody = new
                {
                    model = GetModelIdentifier(),
                    messages = messagesArray.ToArray(),
                    max_tokens = 1000,
                    temperature = 0.7,
                    stream = true,
                    include_reasoning = context.UseReasoning
                };

                // Create HTTP request message
                var jsonContent = JsonSerializer.Serialize(requestBody, _jsonOptions);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/chat/completions")
                {
                    Content = content
                };

                // Send request and process streaming response
                var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();

                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (line.StartsWith("data: "))
                    {
                        var data = line.Substring(6);

                        // Check for "[DONE]" message
                        if (data == "[DONE]")
                        {
                            callback("", true);
                            break;
                        }

                        try
                        {
                            using var jsonDoc = JsonDocument.Parse(data);
                            var root = jsonDoc.RootElement;

                            if (root.TryGetProperty("choices", out var choices) &&
                                choices.ValueKind == JsonValueKind.Array &&
                                choices.GetArrayLength() > 0)
                            {
                                var choice = choices[0];

                                if (choice.TryGetProperty("delta", out var delta) &&
                                    delta.TryGetProperty("content", out var content_token))
                                {
                                    var token = content_token.GetString();
                                    if (!string.IsNullOrEmpty(token))
                                    {
                                        // Add a small delay to make streaming more visible
                                        await Task.Delay(10, cancellationToken);
                                        callback(token, false);
                                    }
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // Skip malformed JSON
                            continue;
                        }
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    callback("\n[Generation stopped]", true);
                }
            }
            catch (Exception ex)
            {
                callback($"\nError: {ex.Message}", true);
            }
        }
        public async Task ProcessQueryWithStreamingWithHistoryAsync(
    string query,
    UserContext context,
    List<MessageHistory> conversationHistory,
    StreamingResponseCallback callback,
    CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                callback("I couldn't understand your query. Could you please provide more details?", true);
                return;
            }

            try
            {
                // Construct system prompt with user context
                var systemPrompt = ConstructSystemPromptFromContext(context);

                // Build messages array including conversation history
                var messagesArray = new List<object>();

                // First add the system message
                messagesArray.Add(new { role = "system", content = systemPrompt });

                // Then add all previous messages from history
                if (conversationHistory != null)
                {
                    foreach (var message in conversationHistory)
                    {
                        messagesArray.Add(new
                        {
                            role = message.IsUserMessage ? "user" : "assistant",
                            content = message.Content
                        });
                    }
                }

                // Finally add the current user query
                messagesArray.Add(new { role = "user", content = query });

                // Get model identifier
                string modelId = GetModelIdentifier();

                // Create request object with basic parameters including reasoning flag
                var requestObject = new Dictionary<string, object>
                {
                    ["model"] = modelId,
                    ["messages"] = messagesArray.ToArray(),
                    ["max_tokens"] = 10000,
                    ["temperature"] = 0.7,
                    ["stream"] = true,
                    ["include_reasoning"] = context.UseReasoning
                };

                // If using a Qwen model, update provider preferences to use Hyperbolic
                if (modelId.StartsWith("qwen/"))
                {
                    requestObject["provider"] = new Dictionary<string, object>
                    {
                        ["order"] = new[] { "Hyperbolic", "Parasail", "Fireworks" },
                        ["allow_fallbacks"] = false
                    };

                    Console.WriteLine("Using specified providers for Qwen model with disabled fallbacks");
                }

                // Create HTTP request message
                var jsonContent = JsonSerializer.Serialize(requestObject);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/chat/completions")
                {
                    Content = content
                };

                // Add required headers for OpenRouter
                request.Headers.Add("HTTP-Referer", "https://productivai.app");
                request.Headers.Add("X-Title", _appName);

                // Send request and process streaming response
                var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();

                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (line.StartsWith("data: "))
                    {
                        var data = line.Substring(6);

                        // Log the raw incoming JSON for debugging
                        Console.WriteLine("Received JSON: " + data);

                        // Check for "[DONE]" message
                        if (data == "[DONE]")
                        {
                            callback("", true);
                            break;
                        }

                        try
                        {
                            using var jsonDoc = JsonDocument.Parse(data);
                            var root = jsonDoc.RootElement;

                            if (root.TryGetProperty("choices", out var choices) &&
                                choices.ValueKind == JsonValueKind.Array &&
                                choices.GetArrayLength() > 0)
                            {
                                var choice = choices[0];

                                if (choice.TryGetProperty("delta", out var delta))
                                {
                                    // Process content token if it exists
                                    if (delta.TryGetProperty("content", out var contentToken) &&
                                       contentToken.ValueKind == JsonValueKind.String)
                                    {
                                        var contentText = contentToken.GetString();
                                        if (!string.IsNullOrEmpty(contentText))
                                        {
                                            // Send content token normally
                                            await Task.Delay(10, cancellationToken);
                                            callback(contentText, false);
                                        }
                                    }

                                    // Process reasoning token if it exists
                                    if (delta.TryGetProperty("reasoning", out var reasoningToken) &&
                                        reasoningToken.ValueKind == JsonValueKind.String)
                                    {
                                        var reasoningText = reasoningToken.GetString();
                                        if (!string.IsNullOrEmpty(reasoningText))
                                        {
                                            Console.WriteLine("Reasoning token received: " + reasoningText);

                                            // Send reasoning token with special marker
                                            await Task.Delay(10, cancellationToken);
                                            callback("[REASONING]" + reasoningText + "[/REASONING]", false);
                                        }
                                    }
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // Skip malformed JSON
                            continue;
                        }
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    callback("\n[Generation stopped]", true);
                }
            }
            catch (Exception ex)
            {
                callback($"\nError: {ex.Message}", true);
            }
        }





        // NEW: JavaScript interop for streaming
        public async ValueTask SetupStreamingResponseInJSAsync(string query, UserContext context, DotNetObjectReference<CallbackHandler> callbackRef)
        {
            var systemPrompt = ConstructSystemPromptFromContext(context);

            var requestBody = new
            {
                model = GetModelIdentifier(),
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = query }
                },
                max_tokens = 1000,
                temperature = 0.7,
                stream = true
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, _jsonOptions);

            // Call JavaScript interop method that handles the streaming
            await _jsRuntime.InvokeVoidAsync(
                "productivAIInterop.setupStreamingResponse",
                $"{ApiBaseUrl}/chat/completions",
                _apiKey,
                jsonContent,
                callbackRef
            );
        }

        #region Private Helper Methods

        private string GetModelIdentifier()
        {
            // Map internal model names to OpenRouter model identifiers
            return _modelName.ToLowerInvariant() switch
            {
                "son35" => "anthropic/claude-3-sonnet", // OpenRouter model ID
                "son35-opus" => "anthropic/claude-3-opus",
                "son35-haiku" => "anthropic/claude-3-haiku",
                "deepseekr1" => "deepseek-ai/deepseek-coder",
                "qwen32b" => "qwen/qwq-32b",
                "llama3" => "meta-llama/llama-3-8b-instruct",
                "mixtral" => "mistralai/mixtral-8x7b-instruct",
                "o3mini" => "openai/gpt-3.5-turbo",
                _ => "qwen32b" // Default model
            };
        }

        private string ConstructSystemPromptFromContext(UserContext context)
        {
            if (context == null)
                return "You are a helpful productivity assistant.";

            var sb = new StringBuilder();
            sb.AppendLine("You are a helpful productivity assistant that helps users manage tasks and notes.");

            if (!string.IsNullOrEmpty(context.WorkDescription))
            {
                sb.AppendLine($"\nThe user's work involves: {context.WorkDescription}");
            }

            if (context.FocusAreas?.Any() == true)
            {
                sb.AppendLine($"\nThe user's current focus areas are: {string.Join(", ", context.FocusAreas)}");
            }

            if (context.LongTermGoals?.Any() == true)
            {
                sb.AppendLine($"\nThe user's long-term goals include: {string.Join(", ", context.LongTermGoals)}");
            }

            sb.AppendLine("\nProvide concise, specific, and actionable responses. Prioritize clarity and usefulness in your answers.");

            return sb.ToString();
        }

        private async Task<string> CallAIModelAsync(object requestData)
        {
            // Implementation for calling the OpenRouter API
            var jsonContent = JsonSerializer.Serialize(requestData, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            Exception lastException = null;

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                try
                {
                    // If running in development mode or testing, use a simulated response
                    if (string.IsNullOrEmpty(_apiKey) || _apiKey == "your-api-key-here")
                    {
                        return SimulateAIResponse(jsonContent);
                    }

                    // Make the actual API call to OpenRouter
                    var response = await _httpClient.PostAsync($"{ApiBaseUrl}/chat/completions", content);

                    // Check for success
                    if (response.IsSuccessStatusCode)
                    {
                        var responseJson = await response.Content.ReadAsStringAsync();
                        return responseJson;
                    }

                    // Handle error response
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"API request failed with status {response.StatusCode}: {errorContent}");
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    // Wait before retrying (with exponential backoff)
                    if (attempt < MaxRetries - 1)
                    {
                        await Task.Delay(RetryDelayMs * (int)Math.Pow(2, attempt));
                    }
                }
            }

            // If we get here, all retry attempts failed
            throw new Exception($"Failed to call AI model after {MaxRetries} attempts", lastException);
        }

        private string SimulateAIResponse(string requestJson)
        {
            // Helper method to generate simulated responses for development/testing
            try
            {
                var request = JsonSerializer.Deserialize<JsonElement>(requestJson);
                var messages = request.GetProperty("messages");
                var lastMessage = messages[messages.GetArrayLength() - 1];
                var userContent = lastMessage.GetProperty("content").GetString();

                // OpenRouter response format is different
                if (requestJson.Contains("response_format") && requestJson.Contains("json_object"))
                {
                    if (requestJson.Contains("prioritize") || userContent.Contains("prioritize"))
                    {
                        return "{\"choices\": [{\"message\": {\"content\": {\"prioritized_task_ids\": [\"d2a76c9e-b7f4-4ccf-8c2d-55e9f3a8e4b7\", \"a1b2c3d4-e5f6-4321-a1b2-c3d4e5f67890\"]}}}]}";
                    }

                    if (requestJson.Contains("parse") || userContent.Contains("command"))
                    {
                        return "{\"choices\": [{\"message\": {\"content\": {\"command_type\": \"create_task\", \"parameters\": {\"title\": \"Write project proposal\", \"due_date\": \"next Friday\", \"priority\": \"high\"}}}}]}";
                    }

                    return "{\"choices\": [{\"message\": {\"content\": {\"response\": \"This is a simulated JSON response.\"}}}]}";
                }

                // Simulate task suggestions
                if (userContent.Contains("Focus Areas:") || requestJson.Contains("suggestion"))
                {
                    return "{\"choices\": [{\"message\": {\"content\": \"Here are some task suggestions:\\n1. Schedule a weekly review meeting to track project progress\\n2. Create a presentation for the upcoming client meeting\\n3. Research new productivity tools that could streamline workflow\\n4. Reach out to team members for status updates\"}}]}";
                }

                // Simulate note enhancement
                if (userContent.Contains("Note content:"))
                {
                    var originalNote = userContent.Replace("Note content:", "").Trim();
                    return $"{{\"choices\": [{{\"message\": {{\"content\": \"# Enhanced Note\\n\\n{originalNote}\\n\\n## Key Points\\n- Important concept from your note\\n- Another significant element\\n\\n## Action Items\\n- Consider exploring related topic\\n- Follow up on mentioned resources\"}}}}]}}";
                }

                // Default response
                return "{\"choices\": [{\"message\": {\"content\": \"This is a simulated AI response for development purposes. In production, this would be replaced with actual AI model output.\"}}]}";
            }
            catch
            {
                // Fallback for any parsing errors
                return "{\"choices\": [{\"message\": {\"content\": \"I'm a simulated AI response.\"}}]}";
            }
        }

        private string ParseAIResponse(string responseJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);

                // OpenRouter format: choices[0].message.content
                if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                    choices.ValueKind == JsonValueKind.Array &&
                    choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];

                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var content))
                    {
                        return content.ValueKind == JsonValueKind.String
                            ? content.GetString()
                            : content.ToString();
                    }

                    if (firstChoice.TryGetProperty("text", out var text))
                    {
                        return text.GetString();
                    }
                }

                return "I couldn't process the response.";
            }
            catch
            {
                return null;
            }
        }

        private List<string> ParsePrioritizedTaskIds(string responseJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);

                // Try to extract the prioritized task IDs from OpenRouter response
                if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                    choices.ValueKind == JsonValueKind.Array &&
                    choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var content))
                    {
                        // Content could be a string or an object
                        if (content.ValueKind == JsonValueKind.String)
                        {
                            // Try to parse the content as JSON
                            try
                            {
                                using var contentDoc = JsonDocument.Parse(content.GetString());
                                if (contentDoc.RootElement.TryGetProperty("prioritized_task_ids", out var taskIds) &&
                                    taskIds.ValueKind == JsonValueKind.Array)
                                {
                                    var result = new List<string>();
                                    foreach (var id in taskIds.EnumerateArray())
                                    {
                                        if (id.ValueKind == JsonValueKind.String)
                                        {
                                            result.Add(id.GetString());
                                        }
                                    }
                                    return result;
                                }
                            }
                            catch
                            {
                                // Content wasn't valid JSON, try simple parsing
                                var contentStr = content.GetString();
                                if (contentStr.Contains("[") && contentStr.Contains("]"))
                                {
                                    var extractedList = contentStr.Substring(
                                        contentStr.IndexOf('[') + 1,
                                        contentStr.LastIndexOf(']') - contentStr.IndexOf('[') - 1
                                    );

                                    return extractedList
                                        .Split(',')
                                        .Select(s => s.Trim().Trim('"', '\''))
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .ToList();
                                }
                            }
                        }
                        else if (content.ValueKind == JsonValueKind.Object)
                        {
                            // Direct JSON object in content
                            if (content.TryGetProperty("prioritized_task_ids", out var taskIds) &&
                                taskIds.ValueKind == JsonValueKind.Array)
                            {
                                var result = new List<string>();
                                foreach (var id in taskIds.EnumerateArray())
                                {
                                    if (id.ValueKind == JsonValueKind.String)
                                    {
                                        result.Add(id.GetString());
                                    }
                                }
                                return result;
                            }
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private string ParseCommandResult(string responseJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);

                // Extract the command parsing result from OpenRouter response
                if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                    choices.ValueKind == JsonValueKind.Array &&
                    choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var content))
                    {
                        // If it's a string that might contain JSON
                        if (content.ValueKind == JsonValueKind.String)
                        {
                            try
                            {
                                using var contentDoc = JsonDocument.Parse(content.GetString());
                                var contentRoot = contentDoc.RootElement;

                                var commandType = contentRoot.TryGetProperty("command_type", out var type) ?
                                    type.GetString() : "unknown";

                                var sb = new StringBuilder();
                                sb.AppendLine($"Command Type: {commandType}");

                                if (contentRoot.TryGetProperty("parameters", out var parameters) &&
                                    parameters.ValueKind == JsonValueKind.Object)
                                {
                                    sb.AppendLine("Parameters:");
                                    foreach (var param in parameters.EnumerateObject())
                                    {
                                        sb.AppendLine($"- {param.Name}: {param.Value}");
                                    }
                                }

                                if (contentRoot.TryGetProperty("notes", out var notes) &&
                                    notes.ValueKind == JsonValueKind.String)
                                {
                                    sb.AppendLine($"Notes: {notes.GetString()}");
                                }

                                return sb.ToString();
                            }
                            catch
                            {
                                // If it's not parseable JSON, return the content as is
                                return content.GetString();
                            }
                        }
                        // If it's already a JSON object
                        else if (content.ValueKind == JsonValueKind.Object)
                        {
                            var commandType = content.TryGetProperty("command_type", out var type) ?
                                type.GetString() : "unknown";

                            var sb = new StringBuilder();
                            sb.AppendLine($"Command Type: {commandType}");

                            if (content.TryGetProperty("parameters", out var parameters) &&
                                parameters.ValueKind == JsonValueKind.Object)
                            {
                                sb.AppendLine("Parameters:");
                                foreach (var param in parameters.EnumerateObject())
                                {
                                    sb.AppendLine($"- {param.Name}: {param.Value}");
                                }
                            }

                            if (content.TryGetProperty("notes", out var notes) &&
                                notes.ValueKind == JsonValueKind.String)
                            {
                                sb.AppendLine($"Notes: {notes.GetString()}");
                            }

                            return sb.ToString();
                        }
                    }
                }

                return "I couldn't parse the command structure.";
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}