// Store component references
let inputComponentRef = null;
let selectorComponentRef = null;

// Register the component reference
window.registerInputComponent = function(componentRef) {
    inputComponentRef = componentRef;
};

window.registerSelectorComponent = function(componentRef) {
    selectorComponentRef = componentRef;
};

// Debounce function to limit how often a function can run
function debounce(func, wait) {
    let timeout;
    return function(...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), wait);
    };
}

// Find the actual input element from references
function findInputElement(inputRef) {
    // First try the reference directly
    if (inputRef && inputRef.hasOwnProperty('focus')) {
        return inputRef;
    }
    
    // Then try using querySelector on common MudBlazor input selectors
    const inputSelectors = ['.mud-input-slot', 'input.mud-input', '.mud-input-text'];
    for (const selector of inputSelectors) {
        const input = document.querySelector(selector);
        if (input && window.getComputedStyle(input).display !== 'none') {
            return input;
        }
    }
    
    // Try the active element as a last resort
    if (document.activeElement && 
        (document.activeElement.tagName === 'INPUT' || 
         document.activeElement.tagName === 'TEXTAREA')) {
        return document.activeElement;
    }
    
    console.warn("Could not find input element");
    return null;
}

window.setupKeyboardInterceptor = function(elementId, dotNetRef) {
    const element = document.getElementById(elementId);
    if (!element) return;
    
    element.addEventListener("keydown", function(event) {
        // Only intercept events when the selector is shown
        if (dotNetRef.invokeMethod("ShouldInterceptKeyEvent", event.key)) {
            if (event.key === "ArrowDown" || event.key === "ArrowUp" || 
                event.key === "Enter" || event.key === "Escape") {
                event.preventDefault();
                dotNetRef.invokeMethod("ProcessKeyboardEvent", event.key);
                return false;
            }
        }
        
        // Handle Enter key for message sending
        if (event.key === "Enter" && !event.shiftKey) {
            const selectorVisible = dotNetRef.invokeMethod("IsReferenceSelectorVisible");
            if (!selectorVisible) {
                event.preventDefault();
                dotNetRef.invokeMethod("SendMessageFromJS");
                return false;
            }
        }
    });
};

// Set up the TextCaretUtils with improved error handling
window.TextCaretUtils = {
    // Returns the caret coordinates using a shadow element technique
    getCaretCoordinates: function(input) {
        if (!input) {
            console.warn("No input provided to getCaretCoordinates");
            return null;
        }
        
        if (typeof input.selectionStart !== 'number') {
            console.warn("Input element doesn't have a valid selection position");
            return null;
        }
        
        try {
            // Create mirror element off-screen
            const div = document.createElement('div');
            const style = window.getComputedStyle(input);
            const properties = [
                'boxSizing', 'width', 'height', 'overflowX', 'overflowY',
                'borderTopWidth', 'borderRightWidth', 'borderBottomWidth', 'borderLeftWidth',
                'paddingTop', 'paddingRight', 'paddingBottom', 'paddingLeft',
                'fontStyle', 'fontVariant', 'fontWeight', 'fontStretch', 'fontSize', 'lineHeight',
                'fontFamily', 'textAlign', 'textTransform', 'textIndent', 'whiteSpace'
            ];
            
            properties.forEach(function(prop) {
                div.style[prop] = style[prop];
            });
            
            div.style.position = 'absolute';
            div.style.visibility = 'hidden';
            div.style.top = '0';
            div.style.left = '-9999px';
            document.body.appendChild(div);
            
            // Set mirror text up to caret
            const text = input.value.substring(0, input.selectionStart);
            // Replace spaces with non-breaking spaces
            div.textContent = text.replace(/ /g, "\u00a0");
            
            // Create a marker span
            const span = document.createElement('span');
            span.textContent = '|';
            div.appendChild(span);
            
            const spanRect = span.getBoundingClientRect();
            const inputRect = input.getBoundingClientRect();
            
            document.body.removeChild(div);
            
            return {
                top: spanRect.top - inputRect.top + input.scrollTop,
                left: spanRect.left - inputRect.left + input.scrollLeft,
                height: spanRect.height,
                inputRect: inputRect
            };
        } catch (error) {
            console.error("Error calculating caret position:", error);
            return null;
        }
    }
};

// Position the caret anchor element by element IDs
window.positionCaretAnchorById = function(inputId, anchorId) {
    const input = document.getElementById(inputId);
    const anchor = document.getElementById(anchorId);
    
    if (!input || !anchor) {
        console.warn('Could not find input or anchor element by ID');
        return;
    }
    
    try {
        // Get the input coordinates
        const inputRect = input.getBoundingClientRect();
        const caretPosition = input.selectionStart || 0;
        
        // Create a temporary element to measure text width
        const measureDiv = document.createElement('div');
        measureDiv.style.position = 'absolute';
        measureDiv.style.visibility = 'hidden';
        measureDiv.style.whiteSpace = 'pre';
        measureDiv.style.font = window.getComputedStyle(input).font;
        
        // Get text up to caret position
        const text = input.value.substring(0, caretPosition);
        measureDiv.textContent = text || '';
        document.body.appendChild(measureDiv);
        
        // Calculate caret position
        const textWidth = measureDiv.getBoundingClientRect().width;
        document.body.removeChild(measureDiv);
        
        // Calculate the line number based on input width and text width
        const inputWidth = inputRect.width - 
            (parseFloat(window.getComputedStyle(input).paddingLeft) + 
             parseFloat(window.getComputedStyle(input).paddingRight));
        
        const linesBeforeCaret = Math.floor(textWidth / inputWidth);
        const lineHeight = parseFloat(window.getComputedStyle(input).lineHeight) || 20;
        
        // Position anchor element
        anchor.style.display = 'block';
        anchor.style.position = 'absolute';
        
        // Set position based on input position and caret position
        const caretLeft = textWidth % inputWidth;
        const caretTop = linesBeforeCaret * lineHeight;
        
        // Calculate absolute position
        const absoluteTop = inputRect.top + caretTop + window.scrollY + lineHeight;
        const absoluteLeft = inputRect.left + caretLeft + window.scrollX;
        
        // Apply position
        anchor.style.top = absoluteTop + 'px';
        anchor.style.left = absoluteLeft + 'px';
        
        console.log('Positioned anchor at:', {
            input: inputId,
            anchor: anchorId,
            position: {
                top: anchor.style.top,
                left: anchor.style.left
            }
        });
    } catch (error) {
        console.error('Error positioning caret anchor:', error);
    }
};


// Position reference popover with debounce
window.positionReferencePopover = debounce(function() {
    window.positionCaretAnchor();
}, 50);

// Handle document-wide keyboard events for reference selection
document.addEventListener('keydown', function(e) {
    // If the popover is visible, handle navigational keys
    const popover = document.querySelector('.reference-popover');
    
    if (selectorComponentRef && popover && 
        window.getComputedStyle(popover).display !== 'none') {
        
        const key = e.key;
        if (['ArrowDown', 'ArrowUp', 'Enter', 'Escape'].includes(key)) {
            e.preventDefault(); // Prevent default browser behavior
            selectorComponentRef.invokeMethodAsync('HandleKey', key);
        }
    }
});

// Set caret position in text field
window.setCaretPosition = function(position) {
    try {
        const textField = document.querySelector('.mud-input-slot');
        if (textField && typeof position === 'number') {
            textField.focus();
            textField.setSelectionRange(position, position);
        }
    } catch (error) {
        console.error("Error setting caret position:", error);
    }
};

// Track focus state
document.addEventListener('DOMContentLoaded', function() {
    document.addEventListener('focusin', function(e) {
        if ((e.target.classList.contains('mud-input-text') || 
             e.target.classList.contains('mud-input-slot')) && 
            inputComponentRef) {
            inputComponentRef.invokeMethodAsync('OnFocusChanged', true);
        }
    });
    
    document.addEventListener('focusout', function(e) {
        if ((e.target.classList.contains('mud-input-text') || 
             e.target.classList.contains('mud-input-slot')) && 
            inputComponentRef) {
            inputComponentRef.invokeMethodAsync('OnFocusChanged', false);
        }
    });
});

// Helper function to scroll messages to bottom
window.scrollToBottom = function(element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};