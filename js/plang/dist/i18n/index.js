let messages = {};
export async function setLocale(locale) {
    if (locale.toLowerCase().startsWith("is")) {
        messages = (await import("./is.js")).default;
    }
    else {
        messages = (await import("./en.js")).default;
    }
}
export const t = (id, vars) => {
    const tmpl = messages[id] ?? id;
    return vars ? tmpl.replace(/\{(\w+)\}/g, (_, k) => String(vars[k] ?? "")) : tmpl;
};
// optional
export async function initI18n() {
    await setLocale(navigator.language || "en");
}
//# sourceMappingURL=index.js.map