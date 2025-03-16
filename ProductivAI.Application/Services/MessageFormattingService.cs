using Markdig;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

public class MessageFormattingService
{
    // Combine both operations in one method
    public (string FormattedHtml, List<TaskSuggestion> Tasks) FormatMessageWithTaskExtraction(string content)
    {
        if (string.IsNullOrEmpty(content))
            return ("", new List<TaskSuggestion>());

        var extractedTasks = new List<TaskSuggestion>();

        // 1. Extract tasks
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

        // 2. Format the content
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

        return (html, extractedTasks);
    }

    // Keep a simple formatter for cases where you don't need task extraction
    public string FormatMessageContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return "";

        content = content.Trim();
        content = Regex.Replace(content, @"\n{2,}", "\n");
        content = Regex.Replace(content, @"(\r\n|\r|\n)", "\n");

        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        string html = Markdown.ToHtml(content, pipeline);

        html = Regex.Replace(html, @"<p>\s*</p>", "");
        html = Regex.Replace(html, @"<br>\s*<br>", "<br>");

        return html;
    }
}