import type { HandleCtx } from "../net/ResponseDispatcher";
import type { BaseMessage } from "../types/messages";
export declare class UtilHelper {
    static elementNotFound(cssSelector: string, message: BaseMessage, ctx: HandleCtx): void;
    static hasContentModification(actions?: string[]): boolean | undefined;
    static doDownloadResponse(res: Response): Promise<void>;
    static isDownloadResponse(res: Response): boolean;
    private static getResponseFilename;
    static extractHtmlAndScripts(htmlStr: string): {
        html: HTMLDivElement;
        scripts: HTMLScriptElement[];
        styles: HTMLStyleElement[];
        elementId: HTMLElement | null;
    };
    static hashObject(obj: any): Promise<string>;
}
