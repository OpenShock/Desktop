// Small JS interop helpers for OpenShock Desktop.
window.openshock = window.openshock || {};

// Scrolls the element with the given id to the bottom. Used by the logs console
// for autoscroll. No-op if the element is not present.
window.openshock.scrollToBottom = function (elementId) {
    const el = document.getElementById(elementId);
    if (el) el.scrollTop = el.scrollHeight;
};
