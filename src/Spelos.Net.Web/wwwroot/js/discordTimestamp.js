export function getContext() {
    const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone || null;
    const minutes = -new Date().getTimezoneOffset();
    const sign = minutes >= 0 ? "+" : "-";
    const value = Math.abs(minutes);
    return { timeZone, offsetLabel: `UTC${sign}${String(Math.floor(value / 60)).padStart(2, "0")}:${String(value % 60).padStart(2, "0")}`, offsetMinutes: minutes };
}

export function formatNow() { return new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "medium" }).format(new Date()); }

export function preview(epoch, style) {
    const date = new Date(epoch * 1000);
    if (style === "R") {
        const seconds = epoch - Date.now() / 1000;
        const units = [[86400, "day"], [3600, "hour"], [60, "minute"], [1, "second"]];
        const [size, unit] = units.find(([size]) => Math.abs(seconds) >= size) || units.at(-1);
        return new Intl.RelativeTimeFormat(undefined, { numeric: "auto" }).format(Math.round(seconds / size), unit);
    }
    const options = style === "t" ? { timeStyle: "short" } : style === "T" ? { timeStyle: "medium" } :
        style === "d" ? { dateStyle: "short" } : style === "D" ? { dateStyle: "long" } :
        style === "f" ? { dateStyle: "long", timeStyle: "short" } : style === "F" ? { dateStyle: "full", timeStyle: "short" } :
        style === "s" ? { dateStyle: "short", timeStyle: "short" } : { dateStyle: "short", timeStyle: "medium" };
    return new Intl.DateTimeFormat(undefined, options).format(date);
}

export function previews(epoch) { return Object.fromEntries(["t", "T", "d", "D", "f", "F", "s", "S", "R"].map(style => [style, preview(epoch, style)])); }

export function load(key) { return localStorage.getItem(key); }
export function save(key, value) { localStorage.setItem(key, value); }

export async function copy(text, outputId) {
    try { await navigator.clipboard.writeText(text); return { success: true, instruction: "" }; }
    catch {
        const element = document.getElementById(outputId);
        const selection = window.getSelection();
        const range = document.createRange();
        range.selectNodeContents(element); selection.removeAllRanges(); selection.addRange(range); element.focus();
        return { success: false, instruction: /Mac|iPhone|iPad/.test(navigator.platform) ? "press Cmd+C" : "press Ctrl+C" };
    }
}

export function focusOutput() { document.getElementById("discord-generated-output")?.focus(); }
