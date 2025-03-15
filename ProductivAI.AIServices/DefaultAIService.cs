using ProductivAI.Core.Interfaces;
using ProductivAI.Core.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using ProductivAI.Core;

namespace ProductivAI.AIServices
{
    public class DefaultAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _modelName;
        private readonly JsonSerializerOptions _jsonOptions;

        // Constants for API configuration
        private const string ApiBaseUrl = "https://api.example.com/v1"; // Replace with actual API endpoint
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 1000;

        public DefaultAIService(HttpClient httpClient, string apiKey, string modelName = "Son35")
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _modelName = modelName ?? "Son35";

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            // Configure HttpClient
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }
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
                // Simulate preparing a response based on the query
                string responseText;

                // Get a simple simulated response
                if (query.ToLower().Contains("hello") || query.ToLower().Contains("hi"))
                {
                    responseText = "Hello! I'm your AI assistant. How can I help you today?";
                }
                else if (query.ToLower().Contains("help"))
                {
                    responseText = "I can help you with task management, note organization, and answering your questions. What would you like assistance with?";
                }
                else if (query.ToLower().Contains("task") || query.ToLower().Contains("todo"))
                {
                    responseText = "For task management, you can create new tasks, set due dates, and organize them by priority. Would you like me to help you create a task?";
                }
                else
                {
                    responseText = "I understand your query is about \"" + query + "\". As a productivity assistant, I can help you manage tasks, take notes, and answer questions. What specifically would you like to know about this topic?";
                }

                // Simulate streaming by sending words one by one
                var words = responseText.Split(' ');

                for (int i = 0; i < words.Length; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await Task.Delay(50, cancellationToken); // Simulate network delay

                    // Add spacing except for the first word
                    string token = (i > 0 ? " " : "") + words[i];
                    callback(token, false);
                }

                // Signal completion
                callback("", true);
            }
            catch (Exception ex)
            {
                callback($"An error occurred: {ex.Message}", true);
            }
        }
        // Add the same method to DefaultAIService with simplified implementation for testing
        public async Task<TaskDetectionResult> DetectTaskInMessageAsync(string message, UserContext context)
        {
            // Check for task-like phrases
            bool isTaskLike = message.Contains("need to") ||
                              message.Contains("should") ||
                              message.Contains("have to") ||
                              message.Contains("must") ||
                              message.Contains("todo") ||
                              message.Contains("to do") ||
                              message.Contains("task") ||
                              message.Contains("list");

            if (!isTaskLike) return new TaskDetectionResult { IsTaskLike = false };

            // Basic task detection logic
            var result = new TaskDetectionResult
            {
                IsTaskLike = true,
                Confidence = 0.7,
                SuggestedTitle = message.Length > 30 ? message.Substring(0, 30) + "..." : message,
                SuggestedDescription = message,
                SuggestedPriority = 3
            };

            // Simple date extraction
            if (message.Contains("tomorrow"))
            {
                result.SuggestedDueDate = DateTime.Today.AddDays(1);
            }
            else if (message.Contains("next week"))
            {
                result.SuggestedDueDate = DateTime.Today.AddDays(7);
            }

            return result;
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
                // Simulate a response that references conversation history
                string responseText;

                if (conversationHistory != null && conversationHistory.Count > 0)
                {
                    responseText = $"Based on our conversation (with {conversationHistory.Count} previous messages), I understand you're asking about \"{query}\". ";

                    // Add a more contextual response if there's history
                    if (query.ToLower().Contains("previous") || query.ToLower().Contains("earlier") || query.ToLower().Contains("before"))
                    {
                        var lastAssistantMessage = conversationHistory
                            .LastOrDefault(m => !m.IsUserMessage);

                        if (lastAssistantMessage != null)
                        {
                            responseText += $"Earlier I mentioned \"{lastAssistantMessage.Content.Substring(0, Math.Min(40, lastAssistantMessage.Content.Length))}...\". ";
                        }
                    }

                    responseText += "How can I help you further with this topic?";
                }
                else
                {
                    // Fall back to basic response if no history
                    responseText = $"I understand you're asking about \"{query}\". How can I assist you with this?";
                }

                // Simulate streaming by sending words one by one
                var words = responseText.Split(' ');

                for (int i = 0; i < words.Length; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await Task.Delay(50, cancellationToken); // Simulate network delay

                    // Add spacing except for the first word
                    string token = (i > 0 ? " " : "") + words[i];
                    callback(token, false);
                }

                // Signal completion
                callback("", true);
            }
            catch (Exception ex)
            {
                callback($"An error occurred: {ex.Message}", true);
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
                    temperature = 0.7
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
                    temperature = 0.3
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
                    temperature = 0.7
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
                    temperature = 0.4
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
                    temperature = 0.2
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

        #region Private Helper Methods

        private string GetModelIdentifier()
        {
            // Map internal model names to API model identifiers
            return _modelName.ToLowerInvariant() switch
            {
                "son35" => "son-35-turbo",
                "deepseekr1" => "deepseek-r1-advanced",
                "o3mini" => "o3-mini-latest",
                _ => "son-35-turbo" // Default model
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
            // In a real implementation, this would call the actual AI provider's API
            // This implementation includes retry logic and error handling

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

                    // Make the actual API call
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

                // Simulate different response types based on the request
                if (requestJson.Contains("response_format") && requestJson.Contains("json_object"))
                {
                    if (requestJson.Contains("prioritize") || userContent.Contains("prioritize"))
                    {
                        return "{\"completion\": {\"prioritized_task_ids\": [\"d2a76c9e-b7f4-4ccf-8c2d-55e9f3a8e4b7\", \"a1b2c3d4-e5f6-4321-a1b2-c3d4e5f67890\"]}}";
                    }

                    if (requestJson.Contains("parse") || userContent.Contains("command"))
                    {
                        return "{\"completion\": {\"command_type\": \"create_task\", \"parameters\": {\"title\": \"Write project proposal\", \"due_date\": \"next Friday\", \"priority\": \"high\"}}}";
                    }

                    return "{\"completion\": {\"response\": \"This is a simulated JSON response.\"}}";
                }

                // Simulate task suggestions
                if (userContent.Contains("Focus Areas:") || requestJson.Contains("suggestion"))
                {
                    return "{\"completion\": \"Here are some task suggestions:\\n1. Schedule a weekly review meeting to track project progress\\n2. Create a presentation for the upcoming client meeting\\n3. Research new productivity tools that could streamline workflow\\n4. Reach out to team members for status updates\"}";
                }

                // Simulate note enhancement
                if (userContent.Contains("Note content:"))
                {
                    var originalNote = userContent.Replace("Note content:", "").Trim();
                    return $"{{\"completion\": \"# Enhanced Note\\n\\n{originalNote}\\n\\n## Key Points\\n- Important concept from your note\\n- Another significant element\\n\\n## Action Items\\n- Consider exploring related topic\\n- Follow up on mentioned resources\"}}";
                }

                // Default response
                return "{\"completion\": \"This is a simulated AI response for development purposes. In production, this would be replaced with actual AI model output.\"}";
            }
            catch
            {
                // Fallback for any parsing errors
                return "{\"completion\": \"I'm a simulated AI response.\"}";
            }
        }

        private string ParseAIResponse(string responseJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);

                // Try to extract completion from different possible response formats
                if (doc.RootElement.TryGetProperty("completion", out var completion))
                {
                    return completion.ValueKind == JsonValueKind.String ?
                        completion.GetString() :
                        completion.ToString();
                }

                if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                    choices.ValueKind == JsonValueKind.Array &&
                    choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];

                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var content))
                    {
                        return content.GetString();
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

                // Try to extract the prioritized task IDs
                if (doc.RootElement.TryGetProperty("completion", out var completion))
                {
                    if (completion.ValueKind == JsonValueKind.Object &&
                        completion.TryGetProperty("prioritized_task_ids", out var taskIds) &&
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

                // Alternative formats
                if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                    choices.ValueKind == JsonValueKind.Array &&
                    choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var content))
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

                // Extract the command parsing result
                if (doc.RootElement.TryGetProperty("completion", out var completion))
                {
                    // If it's already a formatted string, return it
                    if (completion.ValueKind == JsonValueKind.String)
                    {
                        return completion.GetString();
                    }

                    // If it's a JSON object, format it nicely
                    if (completion.ValueKind == JsonValueKind.Object)
                    {
                        var commandType = completion.TryGetProperty("command_type", out var type) ?
                            type.GetString() : "unknown";

                        var sb = new StringBuilder();
                        sb.AppendLine($"Command Type: {commandType}");

                        if (completion.TryGetProperty("parameters", out var parameters) &&
                            parameters.ValueKind == JsonValueKind.Object)
                        {
                            sb.AppendLine("Parameters:");
                            foreach (var param in parameters.EnumerateObject())
                            {
                                sb.AppendLine($"- {param.Name}: {param.Value}");
                            }
                        }

                        if (completion.TryGetProperty("notes", out var notes) &&
                            notes.ValueKind == JsonValueKind.String)
                        {
                            sb.AppendLine($"Notes: {notes.GetString()}");
                        }

                        return sb.ToString();
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