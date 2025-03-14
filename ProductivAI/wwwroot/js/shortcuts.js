// shortcuts.js - fixed version
window.shortcutsManager = {
    isTyping: false,
    modalIsOpen: false, // Add a flag to track if a modal is open

    registerShortcuts: function (dotNetReference) {
        // Focus tracking
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

        // Modal tracking - track clicks on modal-backdrop and modal elements
        document.addEventListener('click', function (e) {
            // Check if clicked element or its parents are modal components
            if (e.target.classList.contains('modal-backdrop') ||
                e.target.classList.contains('modal') ||
                isChildOfClass(e.target, 'modal') ||
                isChildOfClass(e.target, 'modal-content')) {
                window.shortcutsManager.modalIsOpen = true;
                console.log("Modal interaction detected");
            }
        });

        // Key events
        document.addEventListener('keydown', function (event) {
            // Skip all shortcuts if a modal is open
            if (window.shortcutsManager.modalIsOpen) {
                console.log("Shortcuts disabled - modal is open");
                return;
            }

            // Skip shortcuts during typing or when using modifiers
            if (window.shortcutsManager.isTyping ||
                event.ctrlKey || event.altKey || event.metaKey ||
                event.key.startsWith('F') ||
                event.key === 'Tab' ||
                event.key === 'Escape' ||
                event.key === 'CapsLock') {
                return;
            }

            console.log("Shortcut key pressed: " + event.key);

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

        // Helper function to check if element is an input
        function isInputElement(element) {
            if (!element) return false;

            const tagName = element.tagName ? element.tagName.toLowerCase() : '';
            return tagName === 'input' ||
                tagName === 'textarea' ||
                tagName === 'select' ||
                element.isContentEditable === true;
        }

        // Helper function to check if element is a child of a class
        function isChildOfClass(element, className) {
            let current = element;
            while (current) {
                if (current.classList && current.classList.contains(className)) {
                    return true;
                }
                current = current.parentElement;
            }
            return false;
        }

        // Initial check
        if (document.activeElement && isInputElement(document.activeElement)) {
            window.shortcutsManager.isTyping = true;
        }

        // Check for visible modals on initialization
        if (document.querySelector('.modal-backdrop')) {
            window.shortcutsManager.modalIsOpen = true;
            console.log("Modal detected on initialization");
        }

        console.log("Shortcuts manager initialized");
        return true;
    },

    // Methods to manually set states
    setTypingState: function (isTyping) {
        window.shortcutsManager.isTyping = isTyping;
        console.log("Typing state set to: " + isTyping);
    },

    setModalState: function (isOpen) {
        window.shortcutsManager.modalIsOpen = isOpen;
        console.log("Modal state set to: " + isOpen);
    }
};