// ProductivAI JS Interop
window.productivAIInterop = {
    // Set up streaming response from OpenRouter API
    setupStreamingResponse: function (apiUrl, apiKey, requestBody, dotNetHandler) {
        try {
            const xhr = new XMLHttpRequest();
            xhr.open("POST", apiUrl);
            xhr.setRequestHeader("Content-Type", "application/json");
            xhr.setRequestHeader("Authorization", "Bearer " + apiKey);
            xhr.setRequestHeader("HTTP-Referer", "https://productivai.app");
            xhr.setRequestHeader("X-Title", "ProductivAI");

            // Handle streaming response
            xhr.onreadystatechange = function () {
                // Process partial data as it arrives (readyState 3)
                if (xhr.readyState === 3) {
                    let newData = xhr.responseText;

                    // Split response into lines
                    const lines = newData.split('\n');
                    for (const line of lines) {
                        if (line.startsWith('data: ')) {
                            const data = line.substring(6);

                            // Check for stream end
                            if (data === '[DONE]') {
                                dotNetHandler.invokeMethodAsync('OnComplete');
                                continue;
                            }

                            try {
                                const jsonData = JSON.parse(data);
                                if (jsonData.choices && jsonData.choices.length > 0) {
                                    const choice = jsonData.choices[0];

                                    // Extract content from delta (streaming response)
                                    if (choice.delta && choice.delta.content) {
                                        dotNetHandler.invokeMethodAsync('OnToken', choice.delta.content);
                                    }
                                }
                            } catch (e) {
                                console.error('Error parsing streaming data:', e);
                            }
                        }
                    }
                } else if (xhr.readyState === 4) {
                    // Request completed
                    if (xhr.status !== 200) {
                        dotNetHandler.invokeMethodAsync('OnError', 'API request failed with status: ' + xhr.status);
                    } else {
                        dotNetHandler.invokeMethodAsync('OnComplete');
                    }
                }
            };

            // Handle network errors
            xhr.onerror = function () {
                dotNetHandler.invokeMethodAsync('OnError', 'Network error occurred');
            };

            // Send the request
            xhr.send(requestBody);

            // Return an ID that could be used for cleanup
            return Date.now().toString();
        } catch (error) {
            dotNetHandler.invokeMethodAsync('OnError', 'Error setting up streaming: ' + error.message);
            return null;
        }
    },

    // Register keyboard shortcuts
    registerShortcuts: function (dotNetReference) {
        document.addEventListener('keydown', function (event) {
            // Skip shortcuts if typing in an input, textarea, or contentEditable element
            if (event.target.tagName === 'INPUT' ||
                event.target.tagName === 'TEXTAREA' ||
                event.target.isContentEditable) {
                return;
            }

            // Skip shortcuts if modifiers are pressed
            if (event.ctrlKey || event.altKey || event.metaKey) {
                return;
            }

            // 'Q' for quick create
            if (event.key === 'q' || event.key === 'Q') {
                event.preventDefault();
                dotNetReference.invokeMethodAsync('HandleQuickCreateShortcut');
            }

            // 'S' for settings
            if (event.key === 's' || event.key === 'S') {
                event.preventDefault();
                dotNetReference.invokeMethodAsync('HandleSettingsShortcut');
            }

            // 'C' for completed tasks
            if (event.key === 'c' || event.key === 'C') {
                event.preventDefault();
                dotNetReference.invokeMethodAsync('HandleCompletedTasksShortcut');
            }
        });

        // Return true to indicate successful registration
        return true;
    }
};


window.setupTaskMentionDetection = function (textAreaElement, dotNetRef) {
    if (!textAreaElement) return;
    console.log("Task mention detection setup initialized");

    textAreaElement.addEventListener('input', function () {
        const cursorPos = this.selectionStart;
        const text = this.value;

        // Check for @ symbol followed by text
        const beforeCursor = text.substring(0, cursorPos);
        const match = beforeCursor.match(/@([^\s]*)$/);

        if (match) {
            const searchTerm = match[1] || "";
            const rect = this.getBoundingClientRect();
            const atPos = beforeCursor.lastIndexOf('@');

            dotNetRef.invokeMethodAsync('OnTaskMentionTyped', searchTerm, {
                top: rect.top + 30,
                left: rect.left + 20,
                mentionStart: atPos,
                cursorPos: cursorPos
            });
        } else {
            dotNetRef.invokeMethodAsync('HideTaskSuggestions');
        }
    });
};

window.replaceTaskMention = function (textAreaElement, mentionStart, cursorPos, replacement) {
    if (!textAreaElement) return;

    const text = textAreaElement.value;
    const newText = text.substring(0, mentionStart) + replacement +
        text.substring(cursorPos);

    textAreaElement.value = newText;
    textAreaElement.focus();

    const newCursorPos = mentionStart + replacement.length;
    textAreaElement.setSelectionRange(newCursorPos, newCursorPos);
    textAreaElement.dispatchEvent(new Event('input'));
};


// Add this function to find and fix escaped HTML
window.fixEscapedHtml = function () {
    // Find all elements that might contain escaped HTML task buttons
    const messageContents = document.querySelectorAll('.message-content');

    messageContents.forEach(element => {
        // Look for escaped HTML task buttons
        if (element.innerHTML.includes('&lt;div class="task-suggestion-block"&gt;')) {
            // Unescape the HTML
            element.innerHTML = element.innerHTML
                .replace(/&lt;/g, '<')
                .replace(/&gt;/g, '>')
                .replace(/&quot;/g, '"')
                .replace(/&amp;/g, '&');
        }
    });
};

// Resize textarea based on content
window.resizeTextArea = function (textAreaElement) {
    if (textAreaElement) {
        textAreaElement.style.height = 'auto';
        textAreaElement.style.height = textAreaElement.scrollHeight + 'px';
    }
};

window.setupTextAreaEnterKey = function (textAreaElement) {
    if (textAreaElement) {
        textAreaElement.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                // This will trigger any Blazor onkeydown handler attached to the textarea
                const event = new CustomEvent('blazorSubmit');
                textAreaElement.dispatchEvent(event);
            }
        });
    }
};

window.scrollToEnd = function (element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};

// Add scroll event listener to chat container
window.setupChatScroll = function (chatElement, dotNetRef) {
    if (!chatElement) return;

    chatElement.addEventListener('scroll', function () {
        dotNetRef.invokeMethodAsync('HandleChatScroll');
    });
};

// Fix markdown spacing issues
window.fixMarkdownSpacing = function (elementId) {
    const element = document.getElementById(elementId);
    if (!element) return;

    // Remove excess paragraph wrappers in lists
    const listItems = element.querySelectorAll('li');
    for (const item of listItems) {
        if (item.firstElementChild && item.firstElementChild.tagName === 'P') {
            item.innerHTML = item.firstElementChild.innerHTML;
        }
    }

    // Fix nested list spacing
    const nestedLists = element.querySelectorAll('li > ul, li > ol');
    for (const list of nestedLists) {
        list.style.margin = '0.1rem 0';
    }
};

// Check if element is scrolled to bottom
window.isScrolledToBottom = function (element) {
    if (!element) return true;

    const tolerance = 50; // Pixels of tolerance
    return (element.scrollHeight - element.scrollTop - element.clientHeight) <= tolerance;
};

// Clear any existing handlers to prevent duplicates
if (window.taskButtonClickHandler) {
    document.removeEventListener('click', window.taskButtonClickHandler);
}

// Task suggestion handling
window.createTaskFromSuggestion = function () {
    console.log("TASK BUTTON CLICKED - createTaskFromSuggestion called");

    // Call the specific method for showing prefilled task modal
    DotNet.invokeMethodAsync('ProductivAI', 'PrepareTaskModalFromSuggestion')
        .then(() => {
            console.log("Successfully called PrepareTaskModalFromSuggestion");
        })
        .catch(error => {
            console.error("Error calling PrepareTaskModalFromSuggestion:", error);
        });

    // Prevent default action
    return false;
};

// Create a single handler for delegation
window.taskButtonClickHandler = function (e) {
    // Find the task action button by walking up the DOM tree
    let target = e.target;
    while (target != null) {
        if (target.classList && target.classList.contains('task-action-button')) {
            console.log("Task button clicked via event delegation!");
            e.preventDefault();
            e.stopPropagation();
            window.createTaskFromSuggestion();
            return false;
        }
        target = target.parentElement;
    }
};

// Register the single event delegation handler
document.addEventListener('click', window.taskButtonClickHandler);

// These functions are no longer needed or have been refactored
window.createTask = function (taskId) {
    console.log("Creating task with ID: " + taskId);
    DotNet.invokeMethodAsync('ProductivAI', 'CreateTaskFromJS', taskId);
};

// Task helpers
window.taskHelpers = {
    // Store a reference to the .NET component
    dotNetReference: null,

    // Set the reference
    setDotnetReference: function (reference) {
        window.taskHelpers.dotNetReference = reference;
    }
};

// Utility to render Blazor components into DOM nodes
window.renderComponent = function (elementId, component) {
    console.log("Attempting to render component into element: " + elementId);
    // This would be implemented by the Blazor runtime
};

// Fix to ensure event delegation works for all buttons including dynamically added ones
document.addEventListener('DOMContentLoaded', function () {
    console.log("DOM fully loaded - setting up global handlers");
});

// Task mention detection system
