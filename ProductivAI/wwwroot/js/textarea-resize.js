// textarea-resize.js
window.textAreaManager = {
    init: function (textAreaElement) {
        if (!textAreaElement) return;

        // Set initial height
        this.adjustHeight(textAreaElement);

        // Add input event listener to auto-resize as user types
        textAreaElement.addEventListener('input', function () {
            window.textAreaManager.adjustHeight(this);
        });
    },

    adjustHeight: function (textAreaElement) {
        if (!textAreaElement) return;

        // Reset height to get the correct scrollHeight
        textAreaElement.style.height = 'auto';

        // Calculate new height (limited by CSS max-height)
        const newHeight = Math.min(textAreaElement.scrollHeight, 150);

        // Set the new height
        textAreaElement.style.height = newHeight + 'px';
    }
};