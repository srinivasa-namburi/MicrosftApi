// Add this to your wwwroot/js/app.js or create a new JS file and reference it in your _Host.cshtml
window.scrollToBottom = (element) => {
    if (element) {
        element.scrollTop = element.scrollHeight;
        
        // For smooth scrolling
        element.scrollTo({
            top: element.scrollHeight,
            behavior: 'smooth'
        });
        
        // Sometimes the scroll doesn't happen immediately, try again after a small delay
        setTimeout(() => {
            element.scrollTop = element.scrollHeight;
        }, 50);
    }
};

window.scrollChatToBottom = function(elementId) {
    const element = document.querySelector("." + elementId + " .messages-list");
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
}

// Add this method to detect when a user has scrolled up and show a "scroll to bottom" button
window.initializeScrollDetection = (element, dotNetReference) => {
    if (!element) return;
    
    const scrollHandler = () => {
        const isScrolledToBottom = Math.abs(element.scrollHeight - element.clientHeight - element.scrollTop) < 10;
        dotNetReference.invokeMethodAsync('UpdateScrollPosition', isScrolledToBottom);
    };
    
    element.addEventListener('scroll', scrollHandler);
    
    // Also check on window resize
    window.addEventListener('resize', scrollHandler);
    
    // Return an identifier for this handler so we can remove it later
    const id = new Date().getTime();
    if (!window.scrollHandlers) window.scrollHandlers = {};
    window.scrollHandlers[id] = { 
        element, 
        scrollHandler,
        cleanup: () => {
            element.removeEventListener('scroll', scrollHandler);
            window.removeEventListener('resize', scrollHandler);
            delete window.scrollHandlers[id];
        }
    };
    
    return id;
};

// Call this when disposing the component
window.removeScrollDetection = (id) => {
    if (window.scrollHandlers && window.scrollHandlers[id]) {
        window.scrollHandlers[id].cleanup();
    }
};