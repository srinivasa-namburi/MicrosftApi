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