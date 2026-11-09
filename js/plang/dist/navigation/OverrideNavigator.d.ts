import type { HandleCtx } from "../net/ResponseDispatcher";
import type { Plang } from "../Plang";
import type { BaseMessage } from "../types/messages";
export type HeaderKV = {
    key: string;
    value: string;
};
export type HeadersLike = HeaderKV[] | null | undefined;
export type NavigatorOptions = {
    method?: "GET" | "POST";
    body?: unknown;
    replace?: boolean;
    headers?: HeadersLike;
    sourceElement?: Element | string | null;
    beforeActions?: string | undefined | null;
    afterActions?: string | undefined | null;
    signal?: AbortSignal | null;
    popstate?: boolean;
    scroll?: [number, number] | null;
    onSuccess?: (message: BaseMessage, ctx: HandleCtx) => Promise<void> | null;
    onFailure?: (message: BaseMessage, ctx: HandleCtx) => Promise<void> | null;
    submitter?: HTMLButtonElement | undefined;
};
export interface Navigable {
    navigate: (url: string, opts?: NavigatorOptions) => Promise<unknown>;
}
export declare class OverrideNavigator {
    private plang;
    private errorTarget;
    private defaultTarget;
    constructor(plang: Plang);
    private onClick;
    private onSubmit;
    navigate(url: string, options: NavigatorOptions): Promise<unknown>;
    private getOptions;
    private shouldReturn;
    removeErrorTarget(): void;
    setErrorTarget(elementId: string): void;
    removeDefaultTarget(): void;
    setDefaultTarget(elementId: string): void;
}
