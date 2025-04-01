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

// Position the caret anchor element at the current caret position
window.positionCaretAnchor = function(inputRef) {
    const input = findInputElement(inputRef);
    const anchor = document.querySelector('.caret-position-anchor');
    
    if (!input || !anchor) {
        console.warn('Could not find input or anchor element');
        return;
    }
    
    try {
        // Calculate caret position
        const caretInfo = TextCaretUtils.getCaretCoordinates(input);
        if (!caretInfo) {
            console.warn('Could not get caret coordinates');
            return;
        }
        
        // Get absolute position of caret
        const inputRect = caretInfo.inputRect;
        
        // Position anchor element
        anchor.style.display = 'block';
        
        // Get the offsetParent for the anchor
        let parent = anchor.parentElement;
        let offsetX = 0;
        let offsetY = 0;
        
        while (parent && parent !== document.body) {
            offsetX += parent.offsetLeft - parent.scrollLeft;
            offsetY += parent.offsetTop - parent.scrollTop;
            parent = parent.offsetParent;
        }
        
        // Set absolute position relative to the page
        const absoluteTop = inputRect.top + caretInfo.top + caretInfo.height + window.scrollY;
        const absoluteLeft = inputRect.left + caretInfo.left + window.scrollX;
        
        // Apply position accounting for offsets
        anchor.style.position = 'absolute';
        anchor.style.top = (absoluteTop - offsetY) + 'px';
        anchor.style.left = (absoluteLeft - offsetX) + 'px';
        
        console.log('Positioned anchor at:', {
            caretInfo,
            inputRect,
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