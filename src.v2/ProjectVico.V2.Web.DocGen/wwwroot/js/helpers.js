function scrollToBottom(el) {
    setTimeout(() => {
        el.scrollTop = el.scrollHeight;
    }, 100);

}