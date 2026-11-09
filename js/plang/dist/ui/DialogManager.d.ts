import type { Plang } from "../Plang";
export declare class DialogManager {
    private activeDialogs;
    private observers;
    private stylesInjected;
    private targetSel?;
    private plang;
    constructor(plang: Plang);
    private injectStyles;
    showDialog(targetSel: string, isModal?: boolean): HTMLElement;
    private getDialogId;
    private getElementId;
    private createDialog;
    createErrorDiv(targetSel: string): HTMLElement;
    private setupContentObserver;
    private updateTitleFromContent;
    private setupEventListeners;
    private closeDialog;
    private manageFocus;
    setDialogContent(targetSel: string, content: string | HTMLElement): void;
    setDialogTitle(targetSel: string, title: string): void;
    close(): void;
    destroy(targetSel: string): void;
}
