window.appInterop = {
    createLucideIcons: () => {
        if (typeof lucide !== 'undefined') {
            lucide.createIcons();
            console.log('Lucide icons created.');
        } else {
            console.error('Lucide library not found.');
        }
    },
    autoResizeTextArea: (textAreaId, maxHeight) => {
        const textArea = document.getElementById(textAreaId);
        if (textArea) {
            textArea.style.height = 'auto'; // Reset height to recalculate scrollHeight
            textArea.style.overflowY = 'hidden'; // Prevent temporary scrollbar during calculation
            
            let newHeight = textArea.scrollHeight;
            if (maxHeight && newHeight > maxHeight) {
                newHeight = maxHeight;
                textArea.style.overflowY = 'auto'; // Show scrollbar as content exceeds max height
            } else {
                textArea.style.overflowY = 'hidden'; // Hide scrollbar if content is within or fits (or less than max)
            }
            
            textArea.style.height = newHeight + 'px';
        } else {
            console.warn(`autoResizeTextArea: Element with ID '${textAreaId}' not found.`);
        }
    },
    scrollToBottom: (element) => {
        if (element) {
            element.scrollTop = element.scrollHeight;
        }
    },
    streamOpenRouterChat: async function (apiKey, model, messagesJson, siteUrl, siteName, dotNetHelper) {
        const openRouterApiUrl = "https://openrouter.ai/api/v1/chat/completions";
        const requestBody = {
            model: model,
            messages: JSON.parse(messagesJson), // Assuming messagesJson is a JSON string
            stream: true
        };

        try {
            const response = await fetch(openRouterApiUrl, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${apiKey}`,
                    'Content-Type': 'application/json',
                    'HTTP-Referer': siteUrl,
                    'X-Title': siteName
                },
                body: JSON.stringify(requestBody)
            });

            if (!response.ok) {
                const errorText = await response.text();
                dotNetHelper.invokeMethodAsync('StreamError', `API request failed with status ${response.status}: ${errorText}`);
                return;
            }

            const reader = response.body.getReader();
            const decoder = new TextDecoder('utf-8');

            while (true) {
                const { done, value } = await reader.read();
                if (done) {
                    // console.log("JS: Stream finished");
                    dotNetHelper.invokeMethodAsync('StreamEnd');
                    break;
                }

                const chunkLines = decoder.decode(value, { stream: true }).split('\n');
                for (const line of chunkLines) {
                    if (line.startsWith('data: ')) {
                        const jsonData = line.substring(6);
                        if (jsonData.trim() === '[DONE]') {
                            // console.log("JS: [DONE] marker received");
                            continue; 
                        }
                        try {
                            const parsedData = JSON.parse(jsonData);
                            const textChunk = parsedData?.choices?.[0]?.delta?.content;
                            const reasoningChunk = parsedData?.choices?.[0]?.delta?.reasoning;

                            if (reasoningChunk && (textChunk === null || textChunk === undefined || textChunk === '')) {
                                // console.log("JS: Sending reasoning to .NET:", reasoningChunk);
                                await dotNetHelper.invokeMethodAsync('ReceiveReasoningUpdate', reasoningChunk);
                            } else if (textChunk) { // Ensure textChunk has a value before sending
                                // console.log("JS: Sending content chunk to .NET:", textChunk);
                                await dotNetHelper.invokeMethodAsync('ReceiveChunk', textChunk);
                            }
                        } catch (e) {
                            // console.error("JS: Error parsing JSON chunk:", jsonData, e);
                            // dotNetHelper.invokeMethodAsync('StreamError', `Error parsing JSON chunk: ${e.message}`);
                        }
                    }
                }
            }
        } catch (error) {
            // console.error("JS: Error in streamOpenRouterChat:", error);
            dotNetHelper.invokeMethodAsync('StreamError', error.message || "An unknown error occurred in JavaScript streaming.");
        }
    },
}; 