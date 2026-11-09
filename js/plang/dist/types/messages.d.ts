export type Level = number | string | null;
export type Channel = string | null;
export interface BaseMessage {
    id: string;
    kind: "text" | "render" | "error" | "execute" | "ask" | "stream" | "unknown";
    channel?: Channel;
    level?: Level;
    status?: number | null;
    target?: string | null;
    actions?: string[];
    debug?: any;
}
export interface DomMessage extends BaseMessage {
    dom: Dom;
    content: string;
}
export interface Dom {
    html: HTMLElement;
    scripts: HTMLScriptElement[];
    styles: HTMLStyleElement[];
    elementId: HTMLElement | null;
}
export interface Callback {
    callbackData: object;
    callbackInfo: object;
    path: string;
    signature: object;
    hash: string;
    previousHash?: string;
}
export interface TextMessage extends DomMessage {
    kind: "text";
    skipNewLine: boolean;
}
export interface RenderMessage extends DomMessage {
    kind: "render";
}
export interface AskMessage extends DomMessage {
    kind: "ask";
    callback: Callback;
}
export interface ErrorMessage extends DomMessage {
    kind: "error";
    key?: string | null;
    message?: string | null;
    fix?: string | null;
    links?: unknown;
    callback: Callback;
}
export interface ExecuteMessage extends BaseMessage {
    kind: "execute";
    function: string;
    data: unknown;
}
export interface StreamMessage extends BaseMessage {
    kind: "stream";
    meta?: unknown;
    streamId: string;
    phase: "start" | "chunk" | "end" | "abort";
    text?: string | null;
    bytes?: string | null;
    hasBinary?: boolean;
    hasText?: boolean;
    contentType?: string | null;
    fileName?: string | null;
}
export interface UnknownMessage extends BaseMessage {
    kind: "unknown";
}
export type Message = TextMessage | RenderMessage | ErrorMessage | ExecuteMessage | AskMessage | StreamMessage | UnknownMessage;
