using Markdig;
using ProductivAI.Core.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

public class MessageFormattingService
{
    // Combine both operations in one method
    public (string FormattedHtml, List<TaskSuggestion> Tasks, List<TaskEditSuggestion> TaskEdits) FormatMessageWithTaskExtraction(string content)
    {
        if (string.IsNullOrEmpty(content))
            return ("", new List<TaskSuggestion>(), new List<TaskEditSuggestion>());

        var extractedTasks = new List<TaskSuggestion>();
        var extractedTaskEdits = new List<TaskEditSuggestion>();

        // Extract tasks
        var taskPattern = @"\[TASK:(\{.*?\})\]";
        var matches = Regex.Matches(content, taskPattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count > 1)
            {
                try
                {
                    var jsonStr = match.Groups[1].Value;
                    var taskData = JsonSerializer.Deserialize<TaskSuggestion>(
                        jsonStr,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (taskData != null)
                    {
                        extractedTasks.Add(taskData);
                        content = content.Replace(match.Value, "");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error extracting task: {ex.Message}");
                }
            }
        }

        // Extract task edit suggestions
        var taskEditPattern = @"\[TASK_EDIT:(\{.*?\})\]";
        var editMatches = Regex.Matches(content, taskEditPattern, RegexOptions.Singleline);

        Console.WriteLine($"Found {editMatches.Count} task edit matches in content");

        foreach (Match match in editMatches)
        {
            if (match.Success && match.Groups.Count > 1)
            {
                try
                {
                    var jsonStr = match.Groups[1].Value;
                    Console.WriteLine($"Parsing task edit JSON: {jsonStr}");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        WriteIndented = false
                    };

                    var taskEditData = JsonSerializer.Deserialize<TaskEditSuggestion>(jsonStr, options);

                    if (taskEditData != null)
                    {
                        // Always add the task edit, even with placeholder IDs
                        extractedTaskEdits.Add(taskEditData);
                        content = content.Replace(match.Value, "");
                        Console.WriteLine($"Successfully extracted task edit suggestion");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error extracting task edit: {ex.Message}");
                }
            }
        }

        // Format the content
        content = content.Trim();
        content = Regex.Replace(content, @"\n{2,}", "\n");
        content = Regex.Replace(content, @"(\r\n|\r|\n)", "\n");

        // Use Markdig for conversion
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        string html = Markdown.ToHtml(content, pipeline);

        // Post-process HTML to clean up spacing
        html = Regex.Replace(html, @"<p>\s*</p>", "");
        html = Regex.Replace(html, @"<br>\s*<br>", "<br>");

        return (html, extractedTasks, extractedTaskEdits);
    }


    // Keep a simple formatter for cases where you don't need task extraction
    public string FormatMessageContentWithoutTaskExtraction(string content)
    {
        if (string.IsNullOrEmpty(content))
            return "";

        // Temporarily replace task markers with placeholders during streaming
        content = Regex.Replace(content, @"\[TASK:(\{.*?\})\]", "[Task suggestion being generated...]");
        content = Regex.Replace(content, @"\[TASK_EDIT:(\{.*?\})\]", "[Task edit suggestion being generated...]");

        // Format as normal
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        string html = Markdown.ToHtml(content, pipeline);
        html = Regex.Replace(html, @"<p>\s*</p>", "");
        html = Regex.Replace(html, @"<br>\s*<br>", "<br>");

        return html;
    }

}