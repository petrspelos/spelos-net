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

export function download(text, filename) {
    const url = URL.createObjectURL(new Blob([text], { type: "text/plain;charset=utf-8" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    document.body.append(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}
