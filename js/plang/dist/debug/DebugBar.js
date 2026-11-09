import { errorReporter } from "../net/ErrorReporter.js";
const MAX_REQUESTS = 20;
const MIN_HEIGHT = 100;
const MAX_HEIGHT = 800;
const DEFAULT_HEIGHT = 350;
const REQUEST_TIMEOUT_MS = 5000; // New request if gap > 5 seconds
const DB_NAME = 'PlangDebugBar';
const DB_STORE = 'starredRequests';
export class DebugBar {
    constructor() {
        this.isExpanded = false;
        this.selectedRequestIndex = 0;
        this.selectedNodeKey = null;
        this.requests = [];
        this.requestIdCounter = 0;
        this.expandedHeight = DEFAULT_HEIGHT;
        this.treeWidth = 350;
        this.isDragging = false;
        this.isDraggingTree = false;
        this.expandedNodes = new Set();
        this.lastSnapshotTime = 0;
        this.db = null;
        this.tooltip = null;
        this.container = this.createContainer();
        document.body.appendChild(this.container);
        this.setupDragResize();
        this.setupTreeResize();
        this.initDb();
    }
    async initDb() {
        try {
            const request = indexedDB.open(DB_NAME, 1);
            request.onupgradeneeded = (e) => {
                const db = e.target.result;
                if (!db.objectStoreNames.contains(DB_STORE)) {
                    db.createObjectStore(DB_STORE, { keyPath: 'id' });
                }
            };
            request.onsuccess = (e) => {
                this.db = e.target.result;
                this.loadStarredRequests();
            };
            request.onerror = () => {
                console.warn('DebugBar: Failed to open IndexedDB');
                errorReporter.report("indexedDb", "DebugBar: failed to open IndexedDB");
            };
        }
        catch (e) {
            // Worth reporting even though this is only the debug bar. The identity keypair
            // lives in IndexedDB too, so a browser that refuses it cannot sign a request,
            // the poll never starts, and the visitor sits on a spinner. This catch is the
            // earliest and cheapest signal that we are in that situation.
            console.warn('DebugBar: IndexedDB not available');
            errorReporter.reportCaught("indexedDb", e);
        }
    }
    async loadStarredRequests() {
        if (!this.db)
            return;
        const tx = this.db.transaction(DB_STORE, 'readonly');
        const store = tx.objectStore(DB_STORE);
        const request = store.getAll();
        request.onsuccess = () => {
            const stored = request.result || [];
            const starredRequests = stored.map(s => ({
                ...s,
                startTime: new Date(s.startTime),
                starred: true
            }));
            // Add starred requests to the end (oldest)
            this.requests = [...this.requests, ...starredRequests];
            // Update counter to avoid id collisions
            const maxId = Math.max(0, ...this.requests.map(r => r.id));
            if (maxId >= this.requestIdCounter) {
                this.requestIdCounter = maxId + 1;
            }
            if (starredRequests.length > 0) {
                this.render();
            }
        };
    }
    async saveStarredRequest(req) {
        if (!this.db)
            return;
        const stored = {
            id: req.id,
            startTime: req.startTime.toISOString(),
            tree: req.tree,
            totalDurationMs: req.totalDurationMs,
            snapshotCount: req.snapshotCount
        };
        const tx = this.db.transaction(DB_STORE, 'readwrite');
        const store = tx.objectStore(DB_STORE);
        store.put(stored);
    }
    async removeStarredRequest(id) {
        if (!this.db)
            return;
        const tx = this.db.transaction(DB_STORE, 'readwrite');
        const store = tx.objectStore(DB_STORE);
        store.delete(id);
    }
    handleMessage(message) {
        try {
            const content = message.content ?? message;
            const callstack = typeof content === 'string' ? JSON.parse(content) : content;
            if (callstack.Frames && callstack.Frames.length > 0) {
                this.addSnapshot(callstack);
                this.render();
                this.show();
            }
        }
        catch (e) {
            console.warn('DebugBar: Failed to parse callstack', e);
            errorReporter.reportCaught("debugBarCallstack", e);
        }
    }
    addSnapshot(callstack) {
        const now = Date.now();
        const timeSinceLastSnapshot = now - this.lastSnapshotTime;
        this.lastSnapshotTime = now;
        // Determine if this belongs to current request or starts a new one
        let request = this.requests[0]; // Current request is now first
        const isNewRequest = !request ||
            timeSinceLastSnapshot > REQUEST_TIMEOUT_MS ||
            this.isNewRequestStart(callstack, request);
        if (isNewRequest) {
            request = {
                id: ++this.requestIdCounter,
                startTime: new Date(),
                tree: null,
                totalDurationMs: 0,
                snapshotCount: 0,
                starred: false
            };
            // Add new request at the beginning (newest first)
            this.requests.unshift(request);
            // Remove oldest non-starred if over limit
            if (this.requests.length > MAX_REQUESTS) {
                // Find oldest non-starred request (from the end)
                for (let i = this.requests.length - 1; i >= 0; i--) {
                    if (!this.requests[i].starred) {
                        this.requests.splice(i, 1);
                        break;
                    }
                }
            }
            this.selectedRequestIndex = 0; // Select the new request (now at index 0)
            this.expandedNodes.clear();
        }
        // Merge this snapshot into the request's tree
        this.mergeSnapshotIntoTree(request, callstack);
        request.snapshotCount++;
        // Update total duration (use the max we've seen)
        if (callstack.TotalDurationMs > request.totalDurationMs) {
            request.totalDurationMs = callstack.TotalDurationMs;
        }
    }
    isNewRequestStart(callstack, currentRequest) {
        // If the root frame name is different, it's a new request
        if (!currentRequest.tree)
            return true;
        const newRootName = callstack.Frames[0]?.Name;
        const currentRootName = currentRequest.tree.frame.Name;
        // Different root = definitely new request
        if (newRootName !== currentRootName)
            return true;
        // Check if the new callstack's root duration is less than what we've seen
        // This would indicate a fresh request rather than a continuation
        const newRootDuration = callstack.Frames[0]?.DurationMs ?? 0;
        const currentRootDuration = currentRequest.tree.frame.DurationMs ?? 0;
        if (newRootDuration < currentRootDuration * 0.5 && currentRequest.snapshotCount > 2) {
            return true;
        }
        return false;
    }
    mergeSnapshotIntoTree(request, callstack) {
        const frames = callstack.Frames;
        if (frames.length === 0)
            return;
        // Initialize tree with root if needed
        if (!request.tree) {
            request.tree = this.createNode(frames[0], '0');
            this.expandedNodes.add('0'); // Auto-expand root
        }
        // Update root frame with latest data
        this.updateNodeFrame(request.tree, frames[0]);
        // Merge remaining frames into tree
        let currentNode = request.tree;
        for (let i = 1; i < frames.length; i++) {
            const frame = frames[i];
            const nodeKey = this.generateNodeKey(frames, i);
            // Find or create child node
            let childNode = currentNode.children.find(c => c.key === nodeKey);
            if (!childNode) {
                childNode = this.createNode(frame, nodeKey);
                currentNode.children.push(childNode);
                // Auto-expand first few levels
                if (i < 3) {
                    this.expandedNodes.add(nodeKey);
                }
            }
            else {
                // Update with latest data
                this.updateNodeFrame(childNode, frame);
            }
            currentNode = childNode;
        }
        // Select the deepest node if nothing selected
        if (!this.selectedNodeKey) {
            this.selectedNodeKey = this.generateNodeKey(frames, frames.length - 1);
        }
    }
    createNode(frame, key) {
        return {
            frame: { ...frame },
            children: [],
            key
        };
    }
    updateNodeFrame(node, frame) {
        // Keep the max duration we've seen
        if (frame.DurationMs > node.frame.DurationMs) {
            node.frame.DurationMs = frame.DurationMs;
        }
        // Update steps if we have more complete data
        if (frame.ExecutedSteps.length > node.frame.ExecutedSteps.length) {
            node.frame.ExecutedSteps = [...frame.ExecutedSteps];
        }
        // Update completion status
        if (frame.IsComplete) {
            node.frame.IsComplete = true;
        }
    }
    generateNodeKey(frames, index) {
        // Create a unique key based on the path to this node
        return frames.slice(0, index + 1)
            .map((f, i) => `${i}:${f.Name}:${f.EventType || ''}`)
            .join('/');
    }
    findNodeByKey(node, key) {
        if (!node)
            return null;
        if (node.key === key)
            return node;
        for (const child of node.children) {
            const found = this.findNodeByKey(child, key);
            if (found)
                return found;
        }
        return null;
    }
    clear() {
        // Keep only starred requests
        this.requests = this.requests.filter(r => r.starred);
        this.selectedRequestIndex = 0;
        this.selectedNodeKey = null;
        this.expandedNodes.clear();
        this.lastSnapshotTime = 0;
        this.render();
        if (this.requests.length === 0) {
            this.hide();
        }
    }
    removeRequest(index) {
        const req = this.requests[index];
        if (!req)
            return;
        // If starred, remove from db
        if (req.starred) {
            this.removeStarredRequest(req.id);
        }
        this.requests.splice(index, 1);
        // Adjust selected index
        if (this.selectedRequestIndex >= this.requests.length) {
            this.selectedRequestIndex = Math.max(0, this.requests.length - 1);
        }
        this.selectedNodeKey = null;
        this.render();
        if (this.requests.length === 0) {
            this.hide();
        }
    }
    toggleStar(index) {
        const req = this.requests[index];
        if (!req)
            return;
        req.starred = !req.starred;
        if (req.starred) {
            this.saveStarredRequest(req);
        }
        else {
            this.removeStarredRequest(req.id);
        }
        this.render();
    }
    createContainer() {
        const el = document.createElement('div');
        el.id = 'plang-debug-bar';
        el.innerHTML = `
            <style>
                #plang-debug-bar {
                    position: fixed;
                    bottom: 0;
                    left: 0;
                    right: 0;
                    background: #1e1e1e;
                    color: #d4d4d4;
                    font-family: 'Consolas', 'Monaco', monospace;
                    font-size: 12px;
                    z-index: 99999;
                    display: none;
                    border-top: 2px solid #007acc;
                }
                #plang-debug-bar.visible { display: block; }
                
                .debug-header {
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    padding: 6px 12px;
                    background: #252526;
                    cursor: pointer;
                    user-select: none;
                    position: relative;
                }
                .debug-header:hover { background: #2d2d2d; }
                
                .debug-title {
                    display: flex;
                    align-items: center;
                    gap: 8px;
                }
                .debug-title .icon { color: #007acc; }
                .debug-title .summary { color: #d4d4d4; }
                .debug-title .count { 
                    background: #007acc; 
                    color: white; 
                    padding: 1px 6px; 
                    border-radius: 10px; 
                    font-size: 10px;
                }
                .debug-title .duration { color: #6a9955; margin-left: 8px; }
                
                .debug-controls {
                    position: absolute;
                    right: 12px;
                    display: flex;
                    gap: 8px;
                }
                .debug-controls button {
                    background: transparent;
                    border: 1px solid #3c3c3c;
                    color: #d4d4d4;
                    padding: 2px 8px;
                    cursor: pointer;
                    border-radius: 3px;
                    font-size: 11px;
                }
                .debug-controls button:hover { background: #3c3c3c; }
                .debug-controls button.clear-btn { border-color: #5a3a3a; }
                .debug-controls button.clear-btn:hover { background: #4a2a2a; }
                
                .debug-resize-handle {
                    height: 6px;
                    background: #252526;
                    cursor: ns-resize;
                    display: flex;
                    justify-content: center;
                    align-items: center;
                }
                .debug-resize-handle:hover { background: #3a3a3a; }
                .debug-resize-handle::after {
                    content: '';
                    width: 40px;
                    height: 3px;
                    background: #555;
                    border-radius: 2px;
                }
                .debug-resize-handle:hover::after { background: #007acc; }
                
                .debug-body {
                    display: none;
                    flex-direction: column;
                    overflow: hidden;
                }
                .debug-body.expanded { display: flex; }
                
                .debug-requests {
                    display: flex;
                    gap: 4px;
                    padding: 6px 10px;
                    background: #1a1a1a;
                    border-bottom: 1px solid #3c3c3c;
                    overflow-x: auto;
                    flex-shrink: 0;
                }
                .debug-request-tab {
                    padding: 4px 10px;
                    background: #2d2d2d;
                    border: 1px solid #3c3c3c;
                    border-radius: 3px;
                    cursor: pointer;
                    white-space: nowrap;
                    font-size: 11px;
                    display: flex;
                    align-items: center;
                    gap: 6px;
                }
                .debug-request-tab:hover { background: #3d3d3d; }
                .debug-request-tab.selected { 
                    background: #094771; 
                    border-color: #007acc;
                }
                .debug-request-tab.starred {
                    border-color: #d4a520;
                }
                .debug-request-tab .name { color: #dcdcaa; }
                .debug-request-tab .time { color: #6a9955; margin-left: 6px; }
                .debug-request-tab .snapshots { color: #888; margin-left: 4px; font-size: 9px; }
                .debug-request-tab .tab-star {
                    color: #666;
                    cursor: pointer;
                    font-size: 12px;
                }
                .debug-request-tab .tab-star:hover { color: #d4a520; }
                .debug-request-tab .tab-star.starred { color: #d4a520; }
                .debug-request-tab .tab-close {
                    color: #666;
                    cursor: pointer;
                    font-size: 14px;
                    line-height: 1;
                }
                .debug-request-tab .tab-close:hover { color: #f44; }
                
                .debug-content {
                    display: flex;
                    flex: 1;
                    overflow: hidden;
                    min-height: 0;
                }
                
                .debug-tree {
                    width: 350px;
                    min-width: 150px;
                    max-width: 80%;
                    border-right: 1px solid #3c3c3c;
                    overflow-y: auto;
                    padding: 4px 0;
                    position: relative;
                }
                
                .debug-tree-resize {
                    position: absolute;
                    top: 0;
                    right: 0;
                    width: 6px;
                    height: 100%;
                    cursor: ew-resize;
                    background: transparent;
                }
                .debug-tree-resize:hover {
                    background: #007acc;
                }
                
                .tree-node {
                    user-select: none;
                }
                .tree-node-content {
                    display: flex;
                    align-items: center;
                    padding: 3px 8px;
                    cursor: pointer;
                    border-left: 3px solid transparent;
                }
                .tree-node-content:hover { background: #2d2d2d; }
                .tree-node-content.selected { 
                    background: #094771; 
                    border-left-color: #007acc;
                }
                .tree-node-content.event { opacity: 0.8; }
                
                .tree-toggle {
                    width: 16px;
                    height: 16px;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    color: #888;
                    font-size: 10px;
                    flex-shrink: 0;
                }
                .tree-toggle.has-children { cursor: pointer; color: #ccc; }
                .tree-toggle.has-children:hover { color: #fff; }
                
                .tree-icon {
                    width: 16px;
                    margin-right: 4px;
                    color: #dcdcaa;
                    font-size: 10px;
                    text-align: center;
                }
                .tree-icon.event { color: #d7ba7d; }
                
                .tree-name { 
                    color: #dcdcaa; 
                    flex: 1;
                    overflow: hidden;
                    text-overflow: ellipsis;
                    white-space: nowrap;
                }
                .tree-node-content.event .tree-name { color: #d7ba7d; }
                
                .tree-badge {
                    font-size: 9px;
                    padding: 1px 4px;
                    border-radius: 2px;
                    margin-left: 6px;
                }
                .tree-badge.before { background: #3a3a00; color: #d7ba7d; }
                .tree-badge.after { background: #003a3a; color: #7dd7d7; }
                
                .tree-duration {
                    color: #6a9955;
                    font-size: 10px;
                    margin-left: 8px;
                    flex-shrink: 0;
                }
                
                .tree-children {
                    margin-left: 16px;
                }
                .tree-children.collapsed { display: none; }
                
                .debug-steps {
                    flex: 1;
                    overflow-y: auto;
                    padding: 8px;
                }
                .debug-step {
                    padding: 6px 10px;
                    margin: 2px 0;
                    border-radius: 3px;
                    border-left: 3px solid transparent;
                }
                .debug-step.complete { 
                    background: #1a2e1a; 
                    border-left-color: #4a4;
                }
                .debug-step.current { 
                    background: #2e2e1a; 
                    border-left-color: #aa4;
                }
                .debug-step.error { 
                    background: #2e1a1a; 
                    border-left-color: #a44;
                }
                .debug-step .text { 
                    color: #d4d4d4;
                    word-break: break-word;
                }
                .debug-step .step-edit-icon {
                    color: #666;
                    text-decoration: none;
                    cursor: pointer;
                    padding: 0 4px;
                }
                .debug-step .step-edit-icon:hover {
                    color: #007acc;
                }
                .debug-step .info {
                    display: flex;
                    gap: 12px;
                    font-size: 10px;
                    color: #888;
                    margin-top: 4px;
                }
                .debug-step .duration { color: #6a9955; }
                .debug-step .return { color: #9cdcfe; }
                .debug-step .line { color: #666; }
                
                .debug-step .step-info-icon {
                    color: #666;
                    cursor: help;
                    margin-left: auto;
                    padding: 0 4px;
                    position: relative;
                }
                .debug-step .step-info-icon:hover { color: #007acc; }
                
                .debug-step .step-tooltip {
                    display: none;
                    position: fixed;
                    background: #2d2d2d;
                    border: 1px solid #007acc;
                    border-radius: 4px;
                    padding: 8px;
                    font-size: 11px;
                    font-family: 'Consolas', 'Monaco', monospace;
                    white-space: pre;
                    max-width: 600px;
                    max-height: 400px;
                    overflow: auto;
                    z-index: 100000;
                    box-shadow: 0 2px 20px rgba(0,0,0,0.7);
                    color: #d4d4d4;
                    pointer-events: none;
                }
                
                .debug-empty {
                    color: #666;
                    padding: 20px;
                    text-align: center;
                }
            </style>
            <div class="debug-header">
                <div class="debug-title">
                    <span class="icon">▶</span>
                    <span class="summary">Debug</span>
                    <span class="count">0</span>
                    <span class="duration"></span>
                </div>
                <div class="debug-controls">
                    <button class="clear-btn">Clear</button>
                    <button class="toggle-btn">Expand</button>
                    <button class="close-btn">×</button>
                </div>
            </div>
            <div class="debug-body">
                <div class="debug-resize-handle"></div>
                <div class="debug-requests"></div>
                <div class="debug-content">
                    <div class="debug-tree"><div class="debug-tree-resize"></div></div>
                    <div class="debug-steps"></div>
                </div>
            </div>
        `;
        el.querySelector('.debug-header')?.addEventListener('click', (e) => {
            if (!e.target.closest('button')) {
                this.toggle();
            }
        });
        el.querySelector('.toggle-btn')?.addEventListener('click', (e) => {
            e.stopPropagation();
            this.toggle();
        });
        el.querySelector('.close-btn')?.addEventListener('click', (e) => {
            e.stopPropagation();
            this.hide();
        });
        el.querySelector('.clear-btn')?.addEventListener('click', (e) => {
            e.stopPropagation();
            this.clear();
        });
        return el;
    }
    render() {
        this.renderHeader();
        this.renderRequests();
        this.renderTree();
        this.renderSteps();
    }
    renderHeader() {
        const count = this.requests.length;
        // Show duration of the latest request (first in array), not total
        const latestMs = this.requests[0]?.totalDurationMs ?? 0;
        this.container.querySelector('.count').textContent = String(count);
        this.container.querySelector('.duration').textContent = count > 0
            ? `${latestMs.toFixed(1)}ms`
            : '';
    }
    renderRequests() {
        const requestsEl = this.container.querySelector('.debug-requests');
        if (this.requests.length === 0) {
            requestsEl.innerHTML = '<div class="debug-empty">No requests</div>';
            return;
        }
        requestsEl.innerHTML = this.requests.map((req, i) => {
            const rootName = req.tree?.frame.Name || 'Request';
            const starClass = req.starred ? 'starred' : '';
            const tabClass = req.starred ? 'starred' : '';
            return `
                <div class="debug-request-tab ${i === this.selectedRequestIndex ? 'selected' : ''} ${tabClass}" 
                     data-index="${i}">
                    <span class="tab-star ${starClass}" data-action="star" title="${req.starred ? 'Unstar' : 'Star'}">★</span>
                    <span class="tab-content">
                        <span class="name">${this.escapeHtml(rootName)}</span>
                        <span class="time">${req.totalDurationMs.toFixed(1)}ms</span>
                        <span class="snapshots">(${req.snapshotCount})</span>
                    </span>
                    <span class="tab-close" data-action="close" title="Remove">×</span>
                </div>
            `;
        }).join('');
        requestsEl.querySelectorAll('.debug-request-tab').forEach(el => {
            const index = parseInt(el.getAttribute('data-index'));
            // Star click
            el.querySelector('.tab-star')?.addEventListener('click', (e) => {
                e.stopPropagation();
                this.toggleStar(index);
            });
            // Close click
            el.querySelector('.tab-close')?.addEventListener('click', (e) => {
                e.stopPropagation();
                this.removeRequest(index);
            });
            // Tab click (select)
            el.addEventListener('click', () => {
                this.selectedRequestIndex = index;
                this.selectedNodeKey = null;
                this.render();
            });
        });
    }
    renderTree() {
        const treeEl = this.container.querySelector('.debug-tree');
        // Apply stored width
        treeEl.style.width = `${this.treeWidth}px`;
        const request = this.requests[this.selectedRequestIndex];
        if (!request?.tree) {
            treeEl.innerHTML = '<div class="debug-tree-resize"></div><div class="debug-empty">No data</div>';
            return;
        }
        treeEl.innerHTML = '<div class="debug-tree-resize"></div>' + this.renderTreeNode(request.tree, 0);
        this.attachTreeListeners(treeEl);
        this.reattachTreeResizeListener();
    }
    reattachTreeResizeListener() {
        const handle = this.container.querySelector('.debug-tree-resize');
        const tree = this.container.querySelector('.debug-tree');
        handle.addEventListener('mousedown', (e) => {
            e.preventDefault();
            this.isDraggingTree = true;
            document.body.style.cursor = 'ew-resize';
            document.body.style.userSelect = 'none';
        });
    }
    renderTreeNode(node, depth) {
        const hasChildren = node.children.length > 0;
        const isExpanded = this.expandedNodes.has(node.key);
        const isSelected = node.key === this.selectedNodeKey;
        const frame = node.frame;
        const toggleIcon = hasChildren
            ? (isExpanded ? '▼' : '▶')
            : '';
        const icon = frame.IsEvent ? '⚡' : '○';
        let badge = '';
        if (frame.IsEvent && frame.EventType) {
            const badgeClass = frame.EventType.toLowerCase() === 'before' ? 'before' : 'after';
            badge = `<span class="tree-badge ${badgeClass}">${frame.EventType}</span>`;
        }
        return `
            <div class="tree-node" data-key="${this.escapeHtml(node.key)}">
                <div class="tree-node-content ${isSelected ? 'selected' : ''} ${frame.IsEvent ? 'event' : ''}"
                     style="padding-left: ${8 + depth * 12}px">
                    <span class="tree-toggle ${hasChildren ? 'has-children' : ''}">${toggleIcon}</span>
                    <span class="tree-icon ${frame.IsEvent ? 'event' : ''}">${icon}</span>
                    <span class="tree-name">${this.escapeHtml(frame.Name)}</span>
                    ${badge}
                    <span class="tree-duration">${frame.DurationMs.toFixed(1)}ms</span>
                </div>
                ${hasChildren ? `
                    <div class="tree-children ${isExpanded ? '' : 'collapsed'}">
                        ${node.children.map(child => this.renderTreeNode(child, depth + 1)).join('')}
                    </div>
                ` : ''}
            </div>
        `;
    }
    attachTreeListeners(treeEl) {
        treeEl.querySelectorAll('.tree-node').forEach(nodeEl => {
            const key = nodeEl.getAttribute('data-key');
            const content = nodeEl.querySelector(':scope > .tree-node-content');
            const toggle = content.querySelector('.tree-toggle');
            // Toggle expand/collapse
            toggle?.addEventListener('click', (e) => {
                e.stopPropagation();
                if (this.expandedNodes.has(key)) {
                    this.expandedNodes.delete(key);
                }
                else {
                    this.expandedNodes.add(key);
                }
                this.renderTree();
            });
            // Select node
            content.addEventListener('click', () => {
                this.selectedNodeKey = key;
                this.renderTree();
                this.renderSteps();
            });
        });
    }
    renderSteps() {
        const stepsEl = this.container.querySelector('.debug-steps');
        const request = this.requests[this.selectedRequestIndex];
        if (!request?.tree) {
            stepsEl.innerHTML = '';
            return;
        }
        const node = this.selectedNodeKey
            ? this.findNodeByKey(request.tree, this.selectedNodeKey)
            : request.tree;
        if (!node) {
            stepsEl.innerHTML = '<div class="debug-empty">Select a node</div>';
            return;
        }
        const frame = node.frame;
        if (!frame.ExecutedSteps.length) {
            stepsEl.innerHTML = `<div class="debug-empty">No steps in "${this.escapeHtml(frame.Name)}"</div>`;
            return;
        }
        stepsEl.innerHTML = frame.ExecutedSteps.map((step, idx) => {
            const isCurrent = !step.IsComplete;
            const classes = [
                'debug-step',
                step.HasError ? 'error' : step.IsComplete ? 'complete' : '',
                isCurrent ? 'current' : ''
            ].filter(Boolean).join(' ');
            // Build VS Code link if we have path info
            const hasPath = frame.AbsolutePath && step.LineNumber > 0;
            const vscodeUrl = hasPath
                ? `vscode://file/${encodeURIComponent(frame.AbsolutePath)}:${step.LineNumber}`
                : null;
            const editIcon = vscodeUrl
                ? `<a href="${vscodeUrl}" class="step-edit-icon" title="Open in VS Code">✎</a>`
                : '';
            return `
                <div class="${classes}">
                    <div class="text">${this.escapeHtml(step.StepText || '(unnamed step)')}</div>
                    <div class="info">
                        <span class="line">line ${step.LineNumber}</span>
                        <span class="duration">${step.DurationMs.toFixed(2)}ms</span>
                        ${step.ReturnValuePreview ? `<span class="return">→ ${this.escapeHtml(step.ReturnValuePreview)}</span>` : ''}
                        ${step.HasError && step.ErrorMessage ? `<span style="color:#f88">${this.escapeHtml(step.ErrorMessage)}</span>` : ''}
                        ${editIcon}
                        <span class="step-info-icon" data-step-idx="${idx}">ⓘ</span>
                    </div>
                </div>
            `;
        }).join('');
        // Setup tooltip on hover
        this.setupStepTooltips(stepsEl, frame.ExecutedSteps);
    }
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
    setupStepTooltips(stepsEl, steps) {
        // Create tooltip element if not exists
        if (!this.tooltip) {
            this.tooltip = document.createElement('div');
            this.tooltip.className = 'step-tooltip';
            this.tooltip.style.cssText = `
                display: none;
                position: fixed;
                background: #2d2d2d;
                border: 1px solid #007acc;
                border-radius: 4px;
                padding: 8px;
                font-size: 11px;
                font-family: 'Consolas', 'Monaco', monospace;
                white-space: pre;
                max-width: 600px;
                max-height: 400px;
                overflow: auto;
                z-index: 100000;
                box-shadow: 0 2px 20px rgba(0,0,0,0.7);
                color: #d4d4d4;
                user-select: text;
                cursor: text;
            `;
            document.body.appendChild(this.tooltip);
            // Keep tooltip visible when hovering over it
            this.tooltip.addEventListener('mouseenter', () => {
                this.tooltip.style.display = 'block';
            });
            this.tooltip.addEventListener('mouseleave', () => {
                this.tooltip.style.display = 'none';
            });
        }
        stepsEl.querySelectorAll('.step-info-icon').forEach((icon) => {
            const idx = parseInt(icon.getAttribute('data-step-idx') || '0');
            const step = steps[idx];
            if (!step)
                return;
            icon.addEventListener('mouseenter', (e) => {
                const stepJson = JSON.stringify(step, null, 2);
                this.tooltip.textContent = stepJson;
                this.tooltip.style.display = 'block';
                const rect = e.target.getBoundingClientRect();
                // Position tooltip first to get its size
                this.tooltip.style.top = '0px';
                this.tooltip.style.left = '0px';
                const tooltipRect = this.tooltip.getBoundingClientRect();
                // Position above the icon, aligned to the right
                let top = rect.top - tooltipRect.height - 4;
                let left = rect.right - tooltipRect.width;
                // Keep within viewport
                if (top < 8) {
                    top = rect.bottom + 4; // Show below instead
                }
                if (left < 8) {
                    left = 8;
                }
                this.tooltip.style.top = `${top}px`;
                this.tooltip.style.left = `${left}px`;
            });
            icon.addEventListener('mouseleave', (e) => {
                // Small delay to allow moving to tooltip
                setTimeout(() => {
                    if (!this.tooltip.matches(':hover')) {
                        this.tooltip.style.display = 'none';
                    }
                }, 100);
            });
        });
    }
    setupDragResize() {
        const handle = this.container.querySelector('.debug-resize-handle');
        const body = this.container.querySelector('.debug-body');
        handle.addEventListener('mousedown', (e) => {
            e.preventDefault();
            this.isDragging = true;
            document.body.style.cursor = 'ns-resize';
            document.body.style.userSelect = 'none';
        });
        document.addEventListener('mousemove', (e) => {
            if (!this.isDragging)
                return;
            const windowHeight = window.innerHeight;
            const mouseY = e.clientY;
            const newHeight = windowHeight - mouseY;
            this.expandedHeight = Math.max(MIN_HEIGHT, Math.min(MAX_HEIGHT, newHeight));
            body.style.height = `${this.expandedHeight}px`;
        });
        document.addEventListener('mouseup', () => {
            if (this.isDragging) {
                this.isDragging = false;
                document.body.style.cursor = '';
                document.body.style.userSelect = '';
            }
        });
    }
    setupTreeResize() {
        const handle = this.container.querySelector('.debug-tree-resize');
        const tree = this.container.querySelector('.debug-tree');
        handle.addEventListener('mousedown', (e) => {
            e.preventDefault();
            this.isDraggingTree = true;
            document.body.style.cursor = 'ew-resize';
            document.body.style.userSelect = 'none';
        });
        document.addEventListener('mousemove', (e) => {
            if (!this.isDraggingTree)
                return;
            const containerRect = this.container.getBoundingClientRect();
            const newWidth = e.clientX - containerRect.left;
            // Clamp between 150px and 80% of container width
            const minWidth = 150;
            const maxWidth = containerRect.width * 0.8;
            this.treeWidth = Math.max(minWidth, Math.min(maxWidth, newWidth));
            tree.style.width = `${this.treeWidth}px`;
        });
        document.addEventListener('mouseup', () => {
            if (this.isDraggingTree) {
                this.isDraggingTree = false;
                document.body.style.cursor = '';
                document.body.style.userSelect = '';
            }
        });
    }
    show() {
        this.container.classList.add('visible');
    }
    hide() {
        this.container.classList.remove('visible');
    }
    toggle() {
        this.isExpanded = !this.isExpanded;
        const body = this.container.querySelector('.debug-body');
        body.classList.toggle('expanded', this.isExpanded);
        if (this.isExpanded) {
            body.style.height = `${this.expandedHeight}px`;
        }
        this.container.querySelector('.toggle-btn').textContent = this.isExpanded ? 'Collapse' : 'Expand';
    }
}
//# sourceMappingURL=DebugBar.js.map