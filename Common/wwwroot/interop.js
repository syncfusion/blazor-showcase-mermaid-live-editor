window.attachResizeEvents = (dotNetHelper) => {
    document.body.style.userSelect = "none";
    function mouseMoveHandler(e) {
        e.preventDefault();
        dotNetHelper.invokeMethodAsync('OnMouseMove', e.clientX);
    }

    function mouseUpHandler(e) {
        dotNetHelper.invokeMethodAsync('OnMouseUp');
        document.body.style.userSelect = "";
        document.removeEventListener('mousemove', mouseMoveHandler);
        document.removeEventListener('mouseup', mouseUpHandler);
    }
    document.addEventListener('mousemove', mouseMoveHandler);
    document.addEventListener('mouseup', mouseUpHandler);
}

window.getWindowWidth = () => {
    return window.innerWidth;
};

window.getTextSize = (text, font) => {
    const canvas = document.createElement("canvas");
    const context = canvas.getContext("2d");
    context.font = "12px Consolas";

    const lines = text.split("\n");
    const lineHeight = 20; // average line height in px
    let maxWidth = 0;

    for (let line of lines) {
        const metrics = context.measureText(line);
        if (metrics.width > maxWidth) {
            maxWidth = metrics.width;
        }
    }

    let width = maxWidth;
    let height = lines.length * lineHeight;

    height += 18; // Add 18px vertical padding
    width += 15; // Add 15px horziontal padding

    return {
        width: width,
        height: height
    };
};

window.getTextSizesBatch = (texts, font) => {
    const canvas = document.createElement("canvas");
    const context = canvas.getContext("2d");
    context.font = font || "12px Consolas";
    const lineHeight = 20; // average line height in px
    const verticalPadding = 18;
    const horizontalPadding = 15;

    const result = {};
    for (let text of texts) {
        const lines = (text || "").split("\n");
        let maxWidth = 0;
        for (let line of lines) {
            const metrics = context.measureText(line);
            if (metrics.width > maxWidth) {
                maxWidth = metrics.width;
            }
        }
        let width = maxWidth + horizontalPadding;
        let height = (lines.length * lineHeight) + verticalPadding;
        result[text] = { width: width, height: height };
    }
    return result;
};

window.copyToClipboard = async function (text) {
    if (!navigator.clipboard) {
        throw new Error("Clipboard API not available");
    }
    await navigator.clipboard.writeText(text);
};

window.exportFile = (data, filename, type) => {
    try {
        let mimeType = "text/plain";
        if (type === "json") mimeType = "application/json";
        else if (type === "xml") mimeType = "application/xml";
        else if (type === "js") mimeType = "application/javascript";

        const blob = new Blob([data], { type: mimeType });
        const link = document.createElement("a");
        link.href = URL.createObjectURL(blob);
        link.download = filename || `data.${type}`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    } catch (err) {
        console.error("Export failed:", err);
    }
};


window.importFile = (dotNetHelper, type) => {
    if (!dotNetHelper || !dotNetHelper.invokeMethodAsync) {
        console.warn("DotNet helper is not ready.");
        return;
    }

    const input = document.createElement("input");
    input.type = "file";
    input.accept = type === "json" ? ".json" : type === "xml" ? ".xml" : ".js";
    input.style.display = "none";

    input.onchange = async (e) => {
        const file = e.target.files[0];
        if (!file) return;

        const reader = new FileReader();
        reader.onload = function () {
            const content = reader.result;

            try {
                dotNetHelper.invokeMethodAsync("ReceiveImportedFile", content);
            } catch (err) {
                console.error("Interop error: ", err);
            }
        };
        reader.readAsText(file);
    };

    document.body.appendChild(input);
    input.click();
    document.body.removeChild(input);
};

window.setNodeStyle = function (domId, stroke, fill, strokeWidth) {
    const element = document.getElementById(domId);
    if (element) {
        element.setAttribute("stroke", stroke);
        element.setAttribute("fill", fill);
        element.setAttribute("stroke-width", strokeWidth);
    }
};



window.setTheme = (themeUrl, color, isDark) => {
    const themeLink = document.getElementById('theme-link');
    if (themeLink) {
        themeLink.href = themeUrl;
    }

    document.body.classList.toggle('dark-theme', isDark);

    const diagram = document.querySelector('.e-diagram');
    if (diagram) {
        diagram.style.backgroundColor = color;
    }
};




window.addSearchKeyboardListener = (dotNetRef) => {
    // Remove existing listener first
    window.removeSearchKeyboardListener();

    window.searchKeyHandler = (event) => {
        if (event.key === 'Enter') {
            dotNetRef.invokeMethodAsync('HandleSearchKeyPress', event.key);
        }
    };
    document.addEventListener('keydown', window.searchKeyHandler);
};

window.removeSearchKeyboardListener = () => {
    if (window.searchKeyHandler) {
        document.removeEventListener('keydown', window.searchKeyHandler);
        window.searchKeyHandler = null;
    }
};

// ----------------------------------------------------------------------------
// Mermaid validation interop
// Mirrors React mermaid.parse + parseError behavior
// ----------------------------------------------------------------------------


window.mermaidInterop = {
    validate: async function (text) {
        if (!window.mermaid) {
            return {
                isValid: false,
                message: "Mermaid is not loaded. Check script order."
            };
        }

        try {
            await mermaid.parse(text);
            return { isValid: true };
        } catch (err) {
            return {
                isValid: false,
                message:
                    err?.message ||
                    err?.hash?.text ||
                    err?.hash?.token ||
                    String(err)
            };
        }
    }
};

window.importMermaidFile = (dotNetRef) => {
    const input = document.createElement("input");
    input.type = "file";
    input.accept = ".mmd,.txt";
    input.onchange = e => {
        const file = e.target.files[0];
        if (!file) return;

        const reader = new FileReader();
            reader.onload = () => {
                // Pass both file content and file name to .NET
                dotNetRef.invokeMethodAsync("ReceiveImportedMermaid", reader.result, file.name);
            };
        reader.readAsText(file);
    };
    input.click();
};

window.exportMermaidFile = (content, fileName) => {
    const blob = new Blob([content], { type: "text/plain" });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(link.href);
};
