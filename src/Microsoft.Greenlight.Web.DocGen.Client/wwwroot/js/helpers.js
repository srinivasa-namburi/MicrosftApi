function preventEnterKey(id) {
    const textarea = document.getElementById(id);
    if (textarea == null) {
        return;
    }
    textarea.addEventListener("keydown",
        function(event) {
            if (event.key === "Enter") {
                if (event.shiftKey) {
                    return;
                }
                event.preventDefault();
            }
        });
}

// Triggers a browser download of a file with given content (string) and contentType.
// Usage from Blazor: JSRuntime.invokeVoidAsync('downloadFileFromText', fileName, content, 'application/json;charset=utf-8');
function downloadFileFromText(fileName, content, contentType) {
    try {
        const blob = new Blob([content], { type: contentType || 'application/octet-stream' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName || 'export.json';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    } catch (e) {
        console.error('downloadFileFromText failed', e);
        alert('Failed to download file: ' + e);
    }
}