export function load(key) {
    try { return localStorage.getItem(key); } catch { return null; }
}

export function save(key, value) {
    try { localStorage.setItem(key, value); } catch { /* Settings simply are not remembered. */ }
}

export async function copy(text, outputId) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        const output = document.getElementById(outputId);
        output?.focus();
        output?.select();
        return false;
    }
}

// Line and column count Unicode characters (code points); a textarea caret counts UTF-16 code units.
export function goTo(inputId, line, column) {
    const input = document.getElementById(inputId);
    if (!input) return;
    const lines = input.value.split("\n");
    const lineIndex = Math.min(Math.max(line, 1), lines.length) - 1;
    let offset = 0;
    for (let i = 0; i < lineIndex; i++) offset += lines[i].length + 1;
    let text = lines[lineIndex];
    // The formatter ignores a leading byte order mark, so its first-line columns start after it.
    if (lineIndex === 0 && text.startsWith("\uFEFF")) { offset += 1; text = text.slice(1); }
    offset += Array.from(text).slice(0, Math.max(column, 1) - 1).join("").length;
    input.focus();
    input.setSelectionRange(offset, offset);
    // Move the caret's line into view: blur and refocus scrolls a textarea to its selection in all engines.
    input.blur();
    input.focus();
}
