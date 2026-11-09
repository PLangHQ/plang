export type Messages = Record<string, string>;
export type T = (id: string, vars?: Record<string, string | number>) => string;
export declare function setLocale(locale: string): Promise<void>;
export declare const t: T;
export declare function initI18n(): Promise<void>;
