// Copyright (c) Microsoft Corporation. All rights reserved.

window.FloatingChat = {
    dotNetRef: null,
    
    initialize: function(dotNetReference) {
        this.dotNetRef = dotNetReference;
        this.setupKeyboardShortcuts();
    },
    
    setupKeyboardShortcuts: function() {
        document.addEventListener('keydown', this.handleKeyDown.bind(this));
    },
    
    handleKeyDown: function(e) {
        if (!this.dotNetRef) return;
        
        // Ctrl+Shift+C to toggle chat
        if (e.ctrlKey && e.shiftKey && e.key === 'C') {
            e.preventDefault();
            this.dotNetRef.invokeMethodAsync('HandleKeyboardShortcut', 'toggle');
        }
        // ESC to close chat
        else if (e.key === 'Escape') {
            e.preventDefault();
            this.dotNetRef.invokeMethodAsync('HandleKeyboardShortcut', 'close');
        }
    },
    
    dispose: function() {
        document.removeEventListener('keydown', this.handleKeyDown);
        this.dotNetRef = null;
    }
};