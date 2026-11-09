// Full file
export type Messages = Record<string, string>;
export type T = (id: string, vars?: Record<string, string | number>) => string;

let messages: Messages = {};

export async function setLocale(locale: string) {
  if (locale.toLowerCase().startsWith("is")) {
    messages = (await import("./is.js")).default;
  } else {
    messages = (await import("./en.js")).default;
  }
}

export const t: T = (id, vars) => {
  const tmpl = messages[id] ?? id;
  return vars ? tmpl.replace(/\{(\w+)\}/g, (_, k) => String(vars[k] ?? "")) : tmpl;
};

// optional
export async function initI18n() {
  await setLocale(navigator.language || "en");
}
