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
            xhr.onreadystatechange = function() {
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
            xhr.onerror = function() {
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
        document.addEventListener('keydown', function(event) {
            // 'Q' for quick create
            if (event.key === 'q' && !event.ctrlKey && !event.altKey && !event.metaKey) {
                dotNetReference.invokeMethodAsync('HandleQuickCreateShortcut');
            }
            
            // 'S' for settings
            if (event.key === 's' && !event.ctrlKey && !event.altKey && !event.metaKey) {
                dotNetReference.invokeMethodAsync('HandleSettingsShortcut');
            }
            
            // 'C' for completed tasks
            if (event.key === 'c' && !event.ctrlKey && !event.altKey && !event.metaKey) {
                dotNetReference.invokeMethodAsync('HandleCompletedTasksShortcut');
            }
        });
        
        // Return true to indicate successful registration
        return true;
    }
};
// Scroll chat messages to bottom


// Resize textarea based on content
window.resizeTextArea = function (textAreaElement) {
    if (textAreaElement) {
        textAreaElement.style.height = 'auto';
        textAreaElement.style.height = textAreaElement.scrollHeight + 'px';
    }
};

// Register keyboard shortcuts
window.productivAIInterop = {
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
        dotNetRef.invokeMethodAsync('OnChatScroll');
    });
};

// Add to your app.js file
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

// Add scroll event listener to chat container
window.setupChatScroll = function (chatElement, dotNetRef) {
    if (!chatElement) return;

    chatElement.addEventListener('scroll', function () {
        dotNetRef.invokeMethodAsync('OnChatScroll');
    });
};

// Also add this helper function if it doesn't exist
window.isScrolledToBottom = function (element) {
    if (!element) return true;

    const tolerance = 50; // Pixels of tolerance
    return (element.scrollHeight - element.scrollTop - element.clientHeight) <= tolerance;
};

window.createTaskFromSuggestion = function () {
    // Get the DotNet reference from the window
    // In app.js
    window.createTaskFromSuggestion = function () {
        DotNet.invokeMethodAsync('ProductivAI', 'ShowTaskSuggestionModal');
    };
};
// Add to app.js or create a new js file and include it in index.html
window.taskHelpers = {
    // Store a reference to the .NET component
    dotNetReference: null,

    // Set the reference
    setDotnetReference: function (reference) {
        window.taskHelpers.dotNetReference = reference;
    },

    // Create task from suggestion
    createTaskFromSuggestion: function () {
        if (window.taskHelpers.dotNetReference) {
            window.taskHelpers.dotNetReference.invokeMethodAsync('ShowTaskSuggestionModal');
        } else {
            console.error("DotNet reference not set");
        }
    }
};