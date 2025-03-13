// shortcuts.js - fixed version
window.shortcutsManager = {
    isTyping: false,

    registerShortcuts: function (dotNetReference) {
        document.addEventListener('focusin', function (e) {
            if (isInputElement(e.target)) {
                window.shortcutsManager.isTyping = true;
            }
        });

        document.addEventListener('focusout', function (e) {
            if (isInputElement(e.target)) {
                window.shortcutsManager.isTyping = false;
            }
        });

        document.addEventListener('keydown', function (event) {
            // Ignore if typing, using modifiers, or if it's a function key
            if (window.shortcutsManager.isTyping ||
                event.ctrlKey || event.altKey || event.metaKey ||
                event.key.startsWith('F') || // Ignore function keys like F12
                event.key === 'Tab' ||
                event.key === 'Escape' ||
                event.key === 'CapsLock') {
                return;
            }

            // Only log shortcut keys for debugging if needed
            // console.log("Shortcut key pressed: " + event.key);

            if (event.key === 'q' || event.key === 'Q') {
                event.preventDefault();
                dotNetReference.invokeMethodAsync('HandleQuickCreateShortcut');
            }

            if (event.key === 's' || event.key === 'S') {
                event.preventDefault();
                dotNetReference.invokeMethodAsync('HandleSettingsShortcut');
            }

            if (event.key === 'c' || event.key === 'C') {
                event.preventDefault();
                dotNetReference.invokeMethodAsync('HandleCompletedTasksShortcut');
            }
        });

        function isInputElement(element) {
            if (!element) return false;

            const tagName = element.tagName ? element.tagName.toLowerCase() : '';
            return tagName === 'input' ||
                tagName === 'textarea' ||
                tagName === 'select' ||
                element.isContentEditable === true;
        }

        // Initial check
        if (document.activeElement && isInputElement(document.activeElement)) {
            window.shortcutsManager.isTyping = true;
        }

        return true;
    },

    setTypingState: function (isTyping) {
        window.shortcutsManager.isTyping = isTyping;
    }
};