import type { TextMessage } from "../../types/messages.js";
export declare class LogNotificationManager {
    private container;
    private mainHeader;
    private mainBadge;
    private groupsContainer;
    private groups;
    private autoCollapseTimer;
    private isMinimized;
    private lastGroupId;
    private groupOrder;
    private maxGroups;
    private manuallyExpanded;
    constructor();
    private init;
    addLogMessage(message: TextMessage): void;
    private collapseAllGroupsExcept;
    private createGroup;
    private createLogEntry;
    private getIconForLevel;
    private show;
    private minimize;
    private expand;
    private resetAutoCollapse;
    private hide;
    clear(): void;
    private injectStyles;
}
