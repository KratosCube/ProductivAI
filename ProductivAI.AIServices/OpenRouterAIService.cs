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
                systemPrompt += @"

When responding to messages, focus primarily on providing helpful conversation and content. Only suggest creating a task when there is a clear reason why the user would benefit from tracking something as a task.

Important guidelines for task suggestions:
- Limit to a maximum of 2 task suggestions per response
- Only suggest tasks for concrete, actionable items
- Tasks should be directly relevant to what the user is discussing
- Don't suggest tasks for routine or trivial matters

When suggesting a task, use the following format:
[TASK:{""title"":""Task title"",""description"":""Task details"",""priority"":3,""dueDate"":null,""subtasks"":[""Subtask 1"",""Subtask 2""]}]

For example:
[TASK:{""title"":""Review quarterly reports"",""description"":""Go through Q1 financial reports before the meeting"",""priority"":4,""dueDate"":""2025-03-20"",""subtasks"":[""Download reports"",""Mark important sections"",""Prepare questions""]}]";
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
                // Construct enhanced system prompt with task suggestion capability
                var systemPrompt = ConstructSystemPromptFromContext(context);

                // Add task awareness and suggestion instructions to the system prompt
                systemPrompt += @"

## TASK AWARENESS
You have access to the user's tasks in two ways:
1. At the beginning of conversations through [CONTEXT_DATA] or [TASK_CONTEXT_UPDATE] sections
2. When the user mentions tasks using @TaskName syntax with [TASK:{...json data...}]

When discussing tasks:
- Reference specific task details (due dates, priorities, subtasks)
- Consider how tasks relate to the user's focus areas and goals
- You can suggest prioritization based on due dates and importance
- Acknowledge dependencies or relationships between mentioned tasks

When suggesting a task, use the following format:
[TASK:{""title"":""Task title"",""description"":""Task details"",""priority"":3,""dueDate"":null,""subtasks"":[""Subtask 1"",""Subtask 2""]}]

For example:
[TASK:{""title"":""Review quarterly reports"",""description"":""Go through Q1 financial reports before the meeting"",""priority"":4,""dueDate"":""2025-03-20"",""subtasks"":[""Download reports"",""Mark important sections"",""Prepare questions""]}]";

                // Add context retention instructions
                systemPrompt += @"

## CONVERSATION CONTEXT RETENTION
IMPORTANT: Always maintain awareness of the full conversation history and context.
You MUST:
1. Reference information from earlier in the conversation
2. Maintain continuity across multiple messages
3. Remember details the user has shared previously
4. Avoid asking for information the user has already provided
5. Build upon previous exchanges rather than treating each message in isolation
6. If you're unsure if something was mentioned before, reference your uncertainty instead of assuming

Example of good context retention:
✓ ""As you mentioned earlier about your project deadline being next Friday...""
✓ ""Building on our previous discussion about your workout routine...""
✓ ""Since you're focusing on improving your Python skills as you told me earlier...""";

                // Add instructions for current query focus
                systemPrompt += @"

## CURRENT QUERY FOCUS
Your primary responsibility is to answer the user's CURRENT QUERY, which will be the most recent message.
- Always prioritize addressing the specific question or request in the current query
- The current query represents what the user wants to know RIGHT NOW
- Use conversation history for context, but focus your response on the current query
- If the current query is a follow-up to earlier discussion, explicitly acknowledge this connection
- Don't summarize the entire conversation history unless specifically asked to do so";

                // DEBUG: Log the system prompt
                Console.WriteLine("=== SYSTEM PROMPT ===");
                Console.WriteLine(systemPrompt);
                Console.WriteLine("=== END SYSTEM PROMPT ===");

                // Build messages array including conversation history
                var messagesArray = new List<object>();

                // First add the system message
                messagesArray.Add(new { role = "system", content = systemPrompt });

                // Track message counts for logging
                int userMessages = 0;
                int assistantMessages = 0;
                int systemMessages = 0;

                // Then add all previous messages from history, filtering out system messages
                if (conversationHistory != null)
                {
                    // DEBUG: Log the conversation history
                    Console.WriteLine($"=== CONVERSATION HISTORY (Count: {conversationHistory.Count}) ===");

                    int messageIndex = 0;
                    foreach (var message in conversationHistory)
                    {
                        // Log message details
                        Console.WriteLine($"Message {messageIndex++}: " +
                            $"IsUserMessage={message.IsUserMessage}, " +
                            $"IsSystemMessage={message.IsSystemMessage == false}, " + // Fixed: Use proper null-conditional
                            $"Length={message.Content?.Length ?? 0}");

                        // Show first 100 chars of content for debugging
                        string contentPreview = message.Content?.Length > 100 ?
                            message.Content.Substring(0, 100) + "..." :
                            message.Content ?? "null";
                        Console.WriteLine($"Content: {contentPreview}");

                        // Skip system messages as they're meant to be invisible context updates
                        // FIXED: The logic was inverted - check if it IS a system message
                        if (message.IsSystemMessage)  // Fixed: correct system message check
                        {
                            Console.WriteLine("Skipping system message");
                            systemMessages++;
                            continue;
                        }

                        // Truncate very long messages to ensure we don't exceed token limits
                        string messageContent = message.Content;
                        if (messageContent != null && messageContent.Length > 8000)
                        {
                            messageContent = messageContent.Substring(0, 8000) + "... [message truncated]";
                        }

                        messagesArray.Add(new
                        {
                            role = message.IsUserMessage ? "user" : "assistant",
                            content = messageContent
                        });

                        // Count messages by type
                        if (message.IsUserMessage)
                            userMessages++;
                        else
                            assistantMessages++;
                    }

                    Console.WriteLine($"Added to context: {userMessages} user msgs, {assistantMessages} assistant msgs, skipped {systemMessages} system msgs");
                    Console.WriteLine("=== END CONVERSATION HISTORY ===");
                }

                // Add a context reminder if we have a significant conversation history
                if (userMessages > 2)
                {
                    messagesArray.Add(new
                    {
                        role = "system",
                        content = "Remember to use the conversation history above for context, but focus your response on the current query below."
                    });
                }

                // Add a marker to emphasize the current query
                messagesArray.Add(new
                {
                    role = "system",
                    content = "IMPORTANT: The following is the user's CURRENT QUERY that you need to focus on answering:"
                });

                // Finally add the current user query with a special marker
                string enhancedQuery = $"[CURRENT QUERY] {query}";
                messagesArray.Add(new { role = "user", content = enhancedQuery });

                // DEBUG: Log the current query
                Console.WriteLine("=== CURRENT QUERY ===");
                Console.WriteLine(enhancedQuery);
                Console.WriteLine("=== END CURRENT QUERY ===");

                // Get model identifier
                string modelId = GetModelIdentifier();

                // Create request object with basic parameters including reasoning flag
                var requestObject = new Dictionary<string, object>
                {
                    ["model"] = modelId,
                    ["messages"] = messagesArray.ToArray(),
                    ["max_tokens"] = 20000,
                    ["temperature"] = 0.7,
                    ["stream"] = true,
                    ["include_reasoning"] = context.UseReasoning
                };

                // If using a Qwen model, update provider preferences to use Hyperbolic
                if (modelId.StartsWith("qwen/"))
                {
                    requestObject["provider"] = new Dictionary<string, object>
                    {
                        ["order"] = new[] { "Groq", "Hyperbolic", "Parasail", "Fireworks" },
                        ["allow_fallbacks"] = false
                    };

                    Console.WriteLine("Using specified providers for Qwen model with disabled fallbacks");
                }

                // Create HTTP request message
                var jsonContent = JsonSerializer.Serialize(requestObject);

                // DEBUG: Log the final JSON request (this is what's sent to the API)
                Console.WriteLine("=== FINAL API REQUEST JSON ===");
                // Format the JSON for better readability
                var options = new JsonSerializerOptions { WriteIndented = true };
                var formattedJson = JsonSerializer.Serialize(requestObject, options);
                Console.WriteLine(formattedJson);
                Console.WriteLine("=== END FINAL API REQUEST JSON ===");

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

        private async Task<string> GenerateConversationSummary(List<MessageHistory> history, UserContext context)
        {
            if (history == null || history.Count < 10)
                return null; // No need to summarize short conversations

            try
            {
                // Create a request to summarize the conversation
                var recentHistory = history.Where(m => !m.IsSystemMessage == true)
                                          .TakeLast(10)
                                          .ToList();

                var summaryRequest = new
                {
                    model = GetModelIdentifier(),
                    messages = new[]
                    {
                new { role = "system", content = "Summarize the key points from this conversation in a concise paragraph. Focus on: 1) User's goals/needs, 2) Important details or preferences shared, 3) Conclusions or decisions reached." },
                new { role = "user", content = JsonSerializer.Serialize(recentHistory.Select(m => new { role = m.IsUserMessage ? "user" : "assistant", content = m.Content })) }
            },
                    max_tokens = 250,
                    temperature = 0.3
                };

                // Make API call to get summary
                var response = await CallAIModelAsync(summaryRequest);

                // Parse and return the summary - fix the variable name conflict here
                if (response != null)
                {
                    using var doc = JsonDocument.Parse(response);
                    if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                        choices[0].TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var contentValue)) // Changed to contentValue
                    {
                        return contentValue.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating conversation summary: {ex.Message}");
            }

            return null;
        }

        // Add this public method to expose the functionality
        public async Task<string> GenerateConversationSummaryAsync(List<MessageHistory> history)
        {
            return await GenerateConversationSummary(history, new UserContext());
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
                "gemini-2" => "google/gemini-2.0-flash-001",
                _ => "qwen32b" // Default model
            };
        }

        private string ConstructSystemPromptFromContext(UserContext context)
        {
            if (context == null)
                return "You are a helpful productivity assistant.";

            var sb = new StringBuilder();
            sb.AppendLine("IMPORTANT: NEVER DISCUSS THE APPLICATION ITSELF");
            sb.AppendLine("=============================================");
            sb.AppendLine();
            sb.AppendLine("1. NEVER mention that this is a productivity app, task management system, or any kind of application");
            sb.AppendLine("2. NEVER discuss how tasks work, or reference example tasks from the system");
            sb.AppendLine("3. NEVER explain the [TASK:{}] format or other formatting used by the system");
            sb.AppendLine("4. NEVER make meta-references to the application's features or design");
            sb.AppendLine("5. NEVER discuss note-taking functionality or organizational features");
            sb.AppendLine();
            sb.AppendLine("Keep all responses focused on the user's specific questions only, without references to the application or its functionality.");
            sb.AppendLine();
            sb.AppendLine("=============================================");
            sb.AppendLine();

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
            // Add this section to your existing method
            sb.AppendLine("\n## CONVERSATION CONTEXT RETENTION");
            sb.AppendLine("IMPORTANT: Always maintain awareness of the full conversation history and context.");
            sb.AppendLine("You MUST:");
            sb.AppendLine("1. Reference information from earlier in the conversation");
            sb.AppendLine("2. Maintain continuity across multiple messages");
            sb.AppendLine("3. Remember details the user has shared previously");
            sb.AppendLine("4. Avoid asking for information the user has already provided");
            sb.AppendLine("5. Build upon previous exchanges rather than treating each message in isolation");
            sb.AppendLine("6. If you're unsure if something was mentioned before, reference your uncertainty instead of assuming");

            sb.AppendLine("\nFor example:");
            sb.AppendLine("✓ \"As you mentioned earlier about your project deadline being next Friday...\"");
            sb.AppendLine("✓ \"Building on our previous discussion about your workout routine...\"");
            sb.AppendLine("✓ \"Since you're focusing on improving your Python skills as you told me earlier...\"");

            // Interactive Task Creation Instructions
            sb.AppendLine("\n## INTERACTIVE TASK CREATION");
            sb.AppendLine("When identifying a potential task opportunity, ask clarifying questions BEFORE creating any task:");

            sb.AppendLine("\n1. ASK SPECIFIC QUESTIONS FIRST:");
            sb.AppendLine("   - Ask about exact schedule (specific days and times)");
            sb.AppendLine("   - Ask about preferences, requirements, and constraints");
            sb.AppendLine("   - Ask about deadlines and priority level");
            sb.AppendLine("   - Break complex questions into individual questions for clearer responses");

            sb.AppendLine("\n2. CREATE DETAILED TASKS AFTER GETTING ANSWERS:");
            sb.AppendLine("   - Use the exact details provided by the user (never generic placeholders)");
            sb.AppendLine("   - Include specific days, times, locations in subtasks");
            sb.AppendLine("   - Make all subtasks concrete and immediately actionable");
            sb.AppendLine("   - Use precise measurements, quantities, and deadlines");

            sb.AppendLine("\n❌ INCORRECT (too generic):");
            sb.AppendLine("   - \"Choose 3 days/times per week that align with schedule\"");
            sb.AppendLine("   - \"Decide which muscle groups to target\"");
            sb.AppendLine("   - \"Set specific goals for the project\"");

            sb.AppendLine("\n✅ CORRECT (specific after asking questions):");
            sb.AppendLine("   - \"Monday 6:30am: Upper body workout at home gym\"");
            sb.AppendLine("   - \"Schedule client meeting for Thursday April 10th at 2pm via Zoom\"");
            sb.AppendLine("   - \"Call Dr. Smith's office (555-123-4567) on Monday to schedule appointment\"");

            sb.AppendLine("\nExample interaction flow:");
            sb.AppendLine("1. User: \"I want to start working out regularly.\"");
            sb.AppendLine("2. Assistant (YOU): [Ask questions first]");
            sb.AppendLine("   \"I can help create a workout plan! To make it specific:");
            sb.AppendLine("    - Which days of the week can you commit to working out?");
            sb.AppendLine("    - What time of day works best for you?");
            sb.AppendLine("    - Do you prefer home workouts or gym sessions?");
            sb.AppendLine("    - What are your main fitness goals?\"");
            sb.AppendLine("3. User: \"I can do Monday, Wednesday, Friday at 6am for 45 minutes at my local gym. I want to build strength.\"");
            sb.AppendLine("4. Assistant (YOU): [Now create specific task with exact information]");
            sb.AppendLine("   \"Based on your schedule, here's a strength training plan:\"");
            sb.AppendLine("   [TASK:{\"title\":\"Three-day strength training program at City Gym\",\"description\":\"Follow a structured strength training program on Monday, Wednesday, and Friday at 6am for 45-minute sessions at City Gym focusing on building overall strength\",\"priority\":4,\"dueDate\":\"2025-04-05\",\"subtasks\":[\"Monday 6:00-6:45am: Chest and triceps - bench press, incline press, dips\",\"Wednesday 6:00-6:45am: Back and biceps - rows, pulldowns, curls\",\"Friday 6:00-6:45am: Legs and shoulders - squats, lunges, shoulder press\",\"Pack gym bag night before each session\",\"Record weights and reps in Strong app after each workout\",\"Schedule form check with trainer Jose in week 2\"]}]\"");

            sb.AppendLine("\n## TASK EDITING");
            sb.AppendLine("You can suggest edits to existing tasks. When the user mentions a task that could benefit from changes, use this format:");
            sb.AppendLine("[TASK_EDIT:{\"originalId\":\"task-guid-here\",\"edited\":{...edited task JSON...}}]");
            sb.AppendLine("\nExample task edit suggestion:");
            sb.AppendLine("[TASK_EDIT:{\"originalId\":\"d2a76c9e-b7f4-4ccf-8c2d-55e9f3a8e4b7\",\"edited\":{\"title\":\"Updated Title\",\"description\":\"Updated description\",\"priority\":4,\"dueDate\":\"2025-04-10\",\"subtasks\":[\"Updated subtask 1\",\"New subtask\"]}}]");
            sb.AppendLine("\nWhen suggesting task edits:");
            sb.AppendLine("- Only suggest meaningful improvements that add value");
            sb.AppendLine("- Highlight the specific changes you're suggesting (e.g., \"I suggest changing the due date to next Friday because...\")");
            sb.AppendLine("- Maintain all important information from the original task");
            // Role-Based Assistance
            sb.AppendLine("\n## ROLE-BASED ASSISTANCE");
            sb.AppendLine("When responding to queries, adopt the appropriate professional role based on the context:");

            sb.AppendLine("\n1. For project management queries → Respond as a Senior Project Manager");
            sb.AppendLine("   - Focus on timelines, resources, stakeholder management, and deliverables");
            sb.AppendLine("   - Use methodologies like Agile, Scrum, or Waterfall as appropriate");

            sb.AppendLine("\n2. For software/technical queries → Respond as a Senior Software Architect");
            sb.AppendLine("   - Emphasize best practices, design patterns, and technical considerations");
            sb.AppendLine("   - Consider scalability, maintainability, and security implications");

            sb.AppendLine("\n3. For business/strategy queries → Respond as a Business Strategy Consultant");
            sb.AppendLine("   - Focus on competitive advantage, market analysis, and business models");
            sb.AppendLine("   - Consider ROI, operational efficiency, and growth opportunities");

            sb.AppendLine("\n4. For educational topics → Respond as an Educational Content Developer");
            sb.AppendLine("   - Structure information in digestible, learning-friendly formats");
            sb.AppendLine("   - Incorporate examples, practice opportunities, and knowledge checks");

            sb.AppendLine("\n5. For lifestyle/home queries → Respond as a Lifestyle Organization Consultant");
            sb.AppendLine("   - Provide systematic approaches to home management and organization");
            sb.AppendLine("   - Consider efficiency, sustainability, and personal well-being");

            sb.AppendLine("\nAdopt the role naturally without explicitly mentioning which role you're taking unless directly asked.");

            // Example Tasks Section with Updated More Specific Examples
            sb.AppendLine("\n## EXAMPLE TASKS");
            sb.AppendLine("Here are examples of well-formatted tasks that represent best practices:");

            // Work/Project Management - Updated with more specificity
            sb.AppendLine("\nExample 1: Project Task");
            sb.AppendLine("[TASK:{\"title\":\"Present Q2 marketing proposal to executive team on April 15\",\"description\":\"Prepare and deliver a 20-minute presentation on the Q2 marketing strategy with focus on the new product line to the executive team in the main conference room. Budget request: $175,000.\",\"priority\":5,\"dueDate\":\"2025-04-15\",\"subtasks\":[\"Collect March campaign metrics from Sarah by April 8\",\"Create slide deck with 15 maximum slides by April 10\",\"Include competitor analysis with focus on Acme Inc. and TechGiant\",\"Schedule 30-minute rehearsal with marketing team on April 12 at 2pm\",\"Book main conference room from 10-11am on April 15\",\"Send presentation to John for executive pre-review by April 13\",\"Prepare one-page handout with key metrics (20 copies)\",\"Confirm A/V equipment with IT department on April 14\"]}]");

            // Academic Task - Keep existing
            sb.AppendLine("\nExample 2: Academic Task");
            sb.AppendLine("[TASK:{\"title\":\"Complete research paper on renewable energy impact\",\"description\":\"Research, outline, and write a 15-page paper on recent innovations in renewable energy technologies with focus on practical applications in urban environments. Include at least 12 scholarly sources and prepare for submission to Professor Williams.\",\"priority\":5,\"dueDate\":\"2025-04-18\",\"subtasks\":[\"Define research question and thesis statement\",\"Gather and organize sources from academic journals\",\"Create detailed outline with section headers\",\"Write first draft focusing on content over style\",\"Create data visualizations and diagrams\",\"Revise draft for clarity and argument strength\",\"Format citations according to APA style\",\"Proofread final document for grammar and spelling\",\"Prepare accompanying presentation slides\"]}]");

            // Home/Garden - Updated with more specificity
            sb.AppendLine("\nExample 3: Home Task");
            sb.AppendLine("[TASK:{\"title\":\"Reorganize home office on Saturday, April 12, 9am-2pm\",\"description\":\"Transform home office into a more functional workspace by reorganizing furniture, installing new shelving, and implementing cable management system. Budget: $250 for supplies from Home Depot.\",\"priority\":3,\"dueDate\":\"2025-04-12\",\"subtasks\":[\"Measure office dimensions and create layout sketch by April 5\",\"Order 3 IKEA KALLAX shelving units (white) by April 7\",\"Purchase cable management kit and desk organizers from Home Depot by April 9\",\"Back up computer files to external drive on April 11 evening\",\"Begin at 9am: clear all items from current desk and shelves\",\"11am: Assemble and install new shelving units against north wall\",\"12pm: Lunch break and assess progress\",\"12:30pm: Reorganize books by category and set up new filing system\",\"1pm: Install cable management system and reconnect all equipment\",\"Take before/after photos for home improvement journal\"]}]");

            // Professional Communication
            sb.AppendLine("\nExample 4: Professional Communication Task");
            sb.AppendLine("[TASK:{\"title\":\"Prepare strategic discussion with CEO about department reorganization\",\"description\":\"Develop comprehensive presentation and talking points for one-on-one meeting with CEO to discuss proposed reorganization of marketing department, including staffing changes, budget implications, and expected performance improvements.\",\"priority\":5,\"dueDate\":\"2025-03-27\",\"subtasks\":[\"Analyze current department structure and identify inefficiencies\",\"Research competitor organizational structures for benchmarking\",\"Create new org chart with clear reporting lines\",\"Develop detailed transition plan with timeline\",\"Prepare cost analysis comparing current vs. proposed structure\",\"Identify potential risks and prepare mitigation strategies\",\"Create one-page executive summary of key benefits\",\"Prepare slide deck with visual representations of changes\",\"Anticipate challenging questions and prepare responses\",\"Schedule preliminary discussion with HR director for feedback\"]}]");

            // Health/Fitness - Updated to be more specific
            sb.AppendLine("\nExample 5: Health Task");
            sb.AppendLine("[TASK:{\"title\":\"Complete 4-week morning strength training program\",\"description\":\"Follow a structured 30-minute morning strength workout on Monday, Wednesday, and Friday at 6:30am using home equipment (dumbbells, resistance bands) to improve upper body strength and posture.\",\"priority\":4,\"dueDate\":\"2025-04-28\",\"subtasks\":[\"Monday 6:30-7:00am: Upper body routine - 3 sets each of push-ups, dumbbell rows, and shoulder presses\",\"Wednesday 6:30-7:00am: Core focused routine - planks, Russian twists, and resistance band pulls\",\"Friday 6:30-7:00am: Full body workout - squats, lunges, and compound movements\",\"Prepare workout clothes and equipment the night before each session\",\"Drink 16oz water before each workout\",\"Track progress in fitness journal with weights and reps after each session\",\"Take progress photos every Sunday morning\",\"Adjust workout difficulty on April 14 based on first two weeks' progress\"]}]");

            // Personal Finance
            sb.AppendLine("\nExample 6: Financial Planning Task");
            sb.AppendLine("[TASK:{\"title\":\"Create comprehensive retirement savings plan\",\"description\":\"Analyze current finances, research options, and develop a detailed retirement savings strategy including investment allocations, contribution schedules, and tax optimization approaches to meet retirement goal of $1.5M by age 60.\",\"priority\":4,\"dueDate\":\"2025-04-30\",\"subtasks\":[\"Calculate retirement needs based on desired lifestyle\",\"Review current retirement account balances and performance\",\"Research tax-advantaged account options (401k, IRA, HSA)\",\"Compare fees and returns across investment platforms\",\"Create monthly contribution schedule with auto-payments\",\"Determine optimal asset allocation based on age and risk tolerance\",\"Set up automatic portfolio rebalancing\",\"Consult with financial advisor on tax optimization strategies\",\"Create annual review process to adjust plan as needed\"]}]");

            // Family/Parenting
            sb.AppendLine("\nExample 7: Family Task");
            sb.AppendLine("[TASK:{\"title\":\"Plan educational summer activities for children\",\"description\":\"Research and schedule enriching summer activities for two children (ages 8 and 11) that balance fun, learning, and physical activity while accommodating parents' work schedules. Focus on STEM, creative arts, and outdoor adventures.\",\"priority\":3,\"dueDate\":\"2025-05-15\",\"subtasks\":[\"Research available summer camps and programs in the area\",\"Interview children about their interests and preferences\",\"Create calendar of registration deadlines and program dates\",\"Budget for program costs and materials\",\"Coordinate with other parents for potential carpooling\",\"Schedule family weekend activities to complement programs\",\"Research rainy day educational activities and resources\",\"Prepare materials for at-home science experiments\",\"Create reading list with library visit schedule\"]}]");

            // Travel Planning
            sb.AppendLine("\nExample 8: Travel Task");
            sb.AppendLine("[TASK:{\"title\":\"Plan 10-day family trip to Italy\",\"description\":\"Research, budget, and create detailed itinerary for family vacation to Italy covering Rome, Florence, and Venice with focus on historical sites, authentic food experiences, and family-friendly activities for two adults and two teenagers.\",\"priority\":3,\"dueDate\":\"2025-06-01\",\"subtasks\":[\"Research flight options and book tickets for best value\",\"Compare hotels, apartments, and B&Bs in each city\",\"Create day-by-day itinerary with attraction tickets\",\"Research transportation between cities (train vs. rental car)\",\"Make restaurant reservations for special meals\",\"Book guided tours for major historical sites\",\"Purchase travel insurance with medical coverage\",\"Create packing list for each family member\",\"Prepare digital and physical copies of important documents\",\"Download offline maps and translation apps\"]}]");

            // Task Awareness Section (keep existing)
            sb.AppendLine("\n## TASK AWARENESS");
            sb.AppendLine("You will receive information about the user's tasks in these formats:");

            sb.AppendLine("\n1. At the beginning of a conversation, you'll receive a [CONTEXT_DATA] section with all active tasks");
            sb.AppendLine("2. When user mentions tasks using @TaskName syntax, you'll see [TASK:{...json data...}] after the mention");

            sb.AppendLine("\nWhen discussing tasks:");
            sb.AppendLine("- Reference specific task details (due dates, priorities, subtasks)");
            sb.AppendLine("- Consider how tasks relate to the user's focus areas and goals");
            sb.AppendLine("- You can suggest prioritization based on due dates and importance");
            sb.AppendLine("- Acknowledge dependencies or relationships between mentioned tasks");

            sb.AppendLine("\nWhen receiving updates about tasks via [TASK_CONTEXT_UPDATE] sections:");
            sb.AppendLine("- Be aware of these changes but don't explicitly mention receiving these updates");
            sb.AppendLine("- Use the latest information in your responses");
            sb.AppendLine("- If a task mentioned by the user has been completed, gently inform them");

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
        private string GetEnhancedSystemPrompt(UserContext context, bool includeTaskSuggestions = true)
        {
            var basePrompt = ConstructSystemPromptFromContext(context);

            if (includeTaskSuggestions)
            {
                basePrompt += @"

When responding to messages, focus primarily on providing helpful conversation and content. Only suggest creating a task when there is a clear reason why the user would benefit from tracking something as a task.

Important guidelines for task suggestions:
- Limit to a maximum of 2 task suggestions per response
- Only suggest tasks for concrete, actionable items
- Tasks should be directly relevant to what the user is discussing
- Don't suggest tasks for routine or trivial matters

When suggesting a task, add a special task suggestion block at the end of your response using this format:

<task-suggestion>
{
  ""title"": ""Clear task title"",
  ""description"": ""Detailed description of what needs to be done"",
  ""priority"": 3,
  ""dueDate"": ""YYYY-MM-DD"",
  ""subtasks"": [""First subtask"", ""Second subtask""]
}
</task-suggestion>

Only add this when the user's message clearly implies an action or task. Focus on creating practical, actionable tasks.";
            }

            return basePrompt;
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