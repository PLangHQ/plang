export class LogNotificationManager {
    constructor() {
        this.container = null;
        this.mainHeader = null;
        this.mainBadge = null;
        this.groupsContainer = null;
        this.groups = new Map();
        this.autoCollapseTimer = null;
        this.isMinimized = false;
        this.lastGroupId = null;
        this.groupOrder = []; // Track order of groups
        this.maxGroups = 5;
        this.manuallyExpanded = false; // Track if user manually expanded
        this.init();
        this.injectStyles();
    }
    init() {
        // Create main container
        this.container = document.createElement('div');
        this.container.id = 'plang-log-notifications';
        this.container.className = 'plang-log-container';
        // Create main header
        this.mainHeader = document.createElement('div');
        this.mainHeader.className = 'plang-log-main-header';
        const headerLeft = document.createElement('div');
        headerLeft.className = 'plang-log-header-left';
        const headerTitle = document.createElement('span');
        headerTitle.textContent = 'Notifications';
        this.mainBadge = document.createElement('span');
        this.mainBadge.className = 'plang-log-main-badge';
        this.mainBadge.textContent = '0';
        headerLeft.appendChild(headerTitle);
        headerLeft.appendChild(this.mainBadge);
        const closeButton = document.createElement('button');
        closeButton.className = 'plang-log-close-btn';
        closeButton.innerHTML = '×';
        closeButton.setAttribute('aria-label', 'Close notifications');
        closeButton.title = 'Close notifications';
        this.mainHeader.appendChild(headerLeft);
        this.mainHeader.appendChild(closeButton);
        // Create groups container
        this.groupsContainer = document.createElement('div');
        this.groupsContainer.className = 'plang-log-groups-container';
        this.container.appendChild(this.mainHeader);
        this.container.appendChild(this.groupsContainer);
        // Click on header left side to toggle minimize/expand
        headerLeft.addEventListener('click', () => {
            if (this.isMinimized) {
                this.expand();
            }
            else {
                this.minimize();
            }
        });
        // Click close button to hide completely
        closeButton.addEventListener('click', (e) => {
            e.stopPropagation();
            this.hide();
        });
        document.body.appendChild(this.container);
    }
    addLogMessage(message) {
        if (!this.container || !this.groupsContainer || !message.id)
            return;
        const groupId = message.id;
        // Check if this is a new group (different from last)
        const isNewGroup = this.lastGroupId !== null && this.lastGroupId !== groupId;
        // Get or create group
        let group = this.groups.get(groupId);
        if (!group) {
            group = this.createGroup(groupId);
            this.groups.set(groupId, group);
            // Insert at the TOP of the container (newest first)
            if (this.groupsContainer.firstChild) {
                this.groupsContainer.insertBefore(group.element, this.groupsContainer.firstChild);
            }
            else {
                this.groupsContainer.appendChild(group.element);
            }
            // Add to order tracking at the beginning
            this.groupOrder.unshift(groupId);
            // Remove oldest groups if we exceed max
            while (this.groupOrder.length > this.maxGroups) {
                const oldestGroupId = this.groupOrder.pop();
                if (oldestGroupId) {
                    const oldestGroup = this.groups.get(oldestGroupId);
                    if (oldestGroup) {
                        oldestGroup.element.remove();
                        this.groups.delete(oldestGroupId);
                    }
                }
            }
            // If this is a new group and we have existing groups, collapse all old groups
            if (isNewGroup) {
                this.collapseAllGroupsExcept(groupId);
            }
        }
        // Add message to group
        const logEntry = this.createLogEntry(message);
        group.messagesContainer.appendChild(logEntry);
        group.count++;
        group.badge.textContent = group.count.toString();
        // Update main badge with count from this (last) group
        if (this.mainBadge) {
            this.mainBadge.textContent = group.count.toString();
        }
        // Update last group ID
        this.lastGroupId = groupId;
        // Show container and reset auto-collapse timer
        this.show();
        this.resetAutoCollapse();
    }
    collapseAllGroupsExcept(exceptGroupId) {
        this.groups.forEach((group, groupId) => {
            if (groupId !== exceptGroupId) {
                group.element.classList.add('collapsed');
                const toggleIcon = group.element.querySelector('.plang-log-toggle');
                if (toggleIcon) {
                    toggleIcon.innerHTML = '▽';
                }
            }
        });
    }
    createGroup(groupId) {
        const groupEl = document.createElement('div');
        groupEl.className = 'plang-log-group';
        groupEl.dataset.groupId = groupId;
        // Header
        const header = document.createElement('div');
        header.className = 'plang-log-group-header';
        const title = document.createElement('div');
        title.className = 'plang-log-group-title';
        const titleText = document.createElement('span');
        titleText.textContent = `Process: ${groupId}`;
        const badge = document.createElement('span');
        badge.className = 'plang-log-badge';
        badge.textContent = '0';
        const toggleIcon = document.createElement('span');
        toggleIcon.className = 'plang-log-toggle';
        toggleIcon.innerHTML = '△';
        title.appendChild(titleText);
        header.appendChild(title);
        header.appendChild(badge);
        header.appendChild(toggleIcon);
        // Messages container
        const messagesContainer = document.createElement('div');
        messagesContainer.className = 'plang-log-messages';
        groupEl.appendChild(header);
        groupEl.appendChild(messagesContainer);
        // Toggle collapse on header click
        header.addEventListener('click', (e) => {
            e.stopPropagation();
            groupEl.classList.toggle('collapsed');
            toggleIcon.innerHTML = groupEl.classList.contains('collapsed') ? '▽' : '△';
        });
        return {
            element: groupEl,
            messagesContainer: messagesContainer,
            badge: badge,
            count: 0,
            groupId: groupId
        };
    }
    createLogEntry(message) {
        const entry = document.createElement('div');
        entry.className = `plang-log-entry plang-log-${message.level || 'info'}`;
        const icon = document.createElement('span');
        icon.className = 'plang-log-icon';
        icon.innerHTML = this.getIconForLevel(message.level);
        const content = document.createElement('span');
        content.className = 'plang-log-content';
        content.textContent = message.content;
        entry.appendChild(icon);
        entry.appendChild(content);
        return entry;
    }
    getIconForLevel(level) {
        const icons = {
            info: 'ℹ️',
            success: '✅',
            warning: '⚠️',
            error: '❌',
            debug: '🔍'
        };
        return icons[level || 'info'] || icons.info;
    }
    show() {
        if (!this.container)
            return;
        if (this.isMinimized) {
            this.expand();
        }
        this.container.classList.add('visible');
    }
    minimize() {
        if (!this.container || !this.mainHeader)
            return;
        this.container.classList.add('minimized');
        this.isMinimized = true;
        // Stop the auto-collapse timer when manually minimized
        if (this.autoCollapseTimer) {
            window.clearTimeout(this.autoCollapseTimer);
            this.autoCollapseTimer = null;
        }
    }
    expand() {
        if (!this.container || !this.mainHeader)
            return;
        this.container.classList.remove('minimized');
        this.isMinimized = false;
        // Reset the timer when manually expanded
        this.resetAutoCollapse();
    }
    resetAutoCollapse() {
        if (this.autoCollapseTimer) {
            window.clearTimeout(this.autoCollapseTimer);
        }
        this.autoCollapseTimer = window.setTimeout(() => {
            this.minimize();
        }, 10000); // 10 seconds
    }
    hide() {
        if (!this.container)
            return;
        this.container.classList.remove('visible');
        // Clear the timer
        if (this.autoCollapseTimer) {
            window.clearTimeout(this.autoCollapseTimer);
            this.autoCollapseTimer = null;
        }
    }
    clear() {
        if (!this.groupsContainer)
            return;
        this.groups.clear();
        this.groupsContainer.innerHTML = '';
        this.lastGroupId = null;
        this.groupOrder = [];
        if (this.mainBadge) {
            this.mainBadge.textContent = '0';
        }
    }
    injectStyles() {
        if (document.getElementById('plang-log-styles'))
            return;
        const style = document.createElement('style');
        style.id = 'plang-log-styles';
        style.textContent = `
            .plang-log-container {
                position: fixed;
                top: 80px;
                right: 20px;
                width: 400px;
                max-height: calc(100vh - 100px);
                z-index: 10000;
                opacity: 0;
                transform: translateX(100%);
                transition: all 0.3s ease;
                background: white;
                border-radius: 8px;
                box-shadow: 0 4px 12px rgba(0,0,0,0.15);
                border: 1px solid #e0e0e0;
                overflow: hidden;
            }

            .plang-log-container.visible {
                opacity: 1;
                transform: translateX(0);
            }

            .plang-log-container.minimized {
                max-height: 50px;
                overflow: hidden;
            }

            .plang-log-main-header {
                display: flex;
                justify-content: space-between;
                align-items: center;
                padding: 14px 16px;
                background: white;
                font-weight: 600;
                font-size: 15px;
                color: #333;
                border-bottom: 2px solid #e0e0e0;
            }

            .plang-log-header-left {
                display: flex;
                align-items: center;
                gap: 10px;
                flex: 1;
                cursor: pointer;
                user-select: none;
            }

            .plang-log-header-left:hover {
                opacity: 0.8;
            }

            .plang-log-close-btn {
                background: none;
                border: none;
                font-size: 28px;
                line-height: 1;
                color: #999;
                cursor: pointer;
                padding: 0;
                width: 24px;
                height: 24px;
                display: flex;
                align-items: center;
                justify-content: center;
                border-radius: 4px;
                transition: all 0.2s;
            }

            .plang-log-close-btn:hover {
                background: #f0f0f0;
                color: #666;
            }

            .plang-log-close-btn:active {
                background: #e0e0e0;
            }

            .plang-log-main-badge {
                background: #2196F3;
                color: white;
                padding: 3px 10px;
                border-radius: 12px;
                font-size: 13px;
                font-weight: bold;
                min-width: 24px;
                text-align: center;
            }

            .plang-log-groups-container {
                max-height: calc(100vh - 150px);
                overflow-y: auto;
                padding: 8px;
            }

            .plang-log-group {
                background: #fafafa;
                border-radius: 6px;
                margin-bottom: 8px;
                overflow: hidden;
                border: 1px solid #e0e0e0;
            }

            .plang-log-group-header {
                display: flex;
                justify-content: space-between;
                align-items: center;
                padding: 10px 12px;
                background: #f5f5f5;
                cursor: pointer;
                user-select: none;
            }

            .plang-log-group-header:hover {
                background: #eeeeee;
            }

            .plang-log-group-title {
                flex: 1;
                font-weight: 500;
                color: #333;
                font-size: 13px;
            }

            .plang-log-badge {
                background: #757575;
                color: white;
                padding: 2px 8px;
                border-radius: 10px;
                font-size: 11px;
                font-weight: bold;
                margin: 0 8px;
                min-width: 20px;
                text-align: center;
            }

            .plang-log-toggle {
                color: #666;
                font-size: 14px;
                transition: transform 0.2s;
                user-select: none;
            }

            .plang-log-group.collapsed .plang-log-messages {
                display: none;
            }

            .plang-log-messages {
                padding: 8px;
                background: white;
            }

            .plang-log-entry {
                display: flex;
                align-items: flex-start;
                gap: 8px;
                padding: 8px;
                margin: 4px 0;
                border-radius: 4px;
                font-size: 13px;
                line-height: 1.4;
            }

            .plang-log-info {
                background: #E3F2FD;
                color: #1565C0;
            }

            .plang-log-success {
                background: #E8F5E9;
                color: #2E7D32;
            }

            .plang-log-warning {
                background: #FFF3E0;
                color: #E65100;
            }

            .plang-log-error {
                background: #FFEBEE;
                color: #C62828;
            }

            .plang-log-debug {
                background: #F3E5F5;
                color: #6A1B9A;
            }

            .plang-log-icon {
                flex-shrink: 0;
                font-size: 16px;
            }

            .plang-log-content {
                flex: 1;
                word-break: break-word;
            }

            .plang-log-groups-container::-webkit-scrollbar {
                width: 6px;
            }

            .plang-log-groups-container::-webkit-scrollbar-track {
                background: #f1f1f1;
                border-radius: 3px;
            }

            .plang-log-groups-container::-webkit-scrollbar-thumb {
                background: #888;
                border-radius: 3px;
            }

            .plang-log-groups-container::-webkit-scrollbar-thumb:hover {
                background: #555;
            }
        `;
        document.head.appendChild(style);
    }
}
//# sourceMappingURL=LogNotificationManager.js.map