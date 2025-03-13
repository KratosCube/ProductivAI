using Microsoft.JSInterop;
using ProductivAI.Core;
using System;

namespace ProductivAI.AIServices
{
    /// <summary>
    /// Handles callbacks from JavaScript for streaming responses.
    /// This class is used with DotNetObjectReference to receive streaming data from JS interop.
    /// </summary>
    public class CallbackHandler
    {
        private readonly StreamingResponseCallback _callback;
        private readonly Action _onDispose;

        /// <summary>
        /// Creates a new instance of the CallbackHandler.
        /// </summary>
        /// <param name="callback">The callback to invoke when receiving tokens or completion signals.</param>
        /// <param name="onDispose">Optional action to execute when the handler is disposed.</param>
        public CallbackHandler(StreamingResponseCallback callback, Action onDispose = null)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _onDispose = onDispose;
        }

        /// <summary>
        /// Called by JavaScript when a new token is received.
        /// </summary>
        /// <param name="token">The token text.</param>
        [JSInvokable]
        public void OnToken(string token)
        {
            _callback(token, false);
        }

        /// <summary>
        /// Called by JavaScript when the response is complete.
        /// </summary>
        [JSInvokable]
        public void OnComplete()
        {
            _callback("", true);
        }

        /// <summary>
        /// Called by JavaScript when an error occurs.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        [JSInvokable]
        public void OnError(string errorMessage)
        {
            _callback($"\nError: {errorMessage}", true);
        }

        /// <summary>
        /// Cleans up resources when the handler is no longer needed.
        /// </summary>
        public void Dispose()
        {
            _onDispose?.Invoke();
        }
    }
}