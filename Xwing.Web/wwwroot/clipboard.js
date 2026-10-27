export function copyText(text) {
    return navigator.clipboard.writeText(text);
}

export function openNewTab(url) {
    window.open(url, "_blank", "noopener,noreferrer");
}
