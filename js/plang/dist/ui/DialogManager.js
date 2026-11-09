export class DialogManager {
    constructor(plang) {
        this.activeDialogs = new Map();
        this.observers = new Map();
        this.stylesInjected = false;
        this.injectStyles();
        this.plang = plang;
    }
    injectStyles() {
        if (this.stylesInjected)
            return;
        const styleId = 'dialog-manager-styles';
        if (document.getElementById(styleId)) {
            this.stylesInjected = true;
            return;
        }
        const style = document.createElement('style');
        style.id = styleId;
        style.textContent = `
            /* Base dialog styles */
            .dialog {
                display: none;
                position: fixed;
                top: 0;
                left: 0;
                width: 100%;
                height: 100%;
                align-items: center;
                justify-content: center;
                z-index: 10000;
                opacity: 0;
                transition: opacity 0.3s ease;
                pointer-events: none;
                padding: 1rem;
                box-sizing: border-box;
            }

            .dialog.show {
                opacity: 1;
                pointer-events: auto;
            }

            /* Modal backdrop */
            .dialog.modal {
                background-color: rgba(0, 0, 0, 0.5);
                backdrop-filter: blur(2px);
            }

            /* Dialog content */
            .dialog-content {
                background: white;
                border-radius: 8px;
                box-shadow: 0 10px 25px rgba(0, 0, 0, 0.2);
                max-width: calc(100vw - 2rem);
                max-height: calc(100vh - 2rem);
                width: fit-content;
                min-width: 300px;
                display: flex;
                flex-direction: column;
                transform: scale(0.9);
                transition: transform 0.3s ease;
                position: relative;
                z-index: 10001;
            }

            .dialog.show .dialog-content {
                transform: scale(1);
            }

            /* Dialog header */
            .dialog-header {
                display: flex;
                justify-content: space-between;
                align-items: center;
                padding: 1rem 1.5rem;
                border-bottom: 1px solid #e5e7eb;
                background: #f9fafb;
                border-radius: 8px 8px 0 0;
                flex-shrink: 0;
            }
            .dialog-error {
                background-color: #7e1c1cff;
                padding:4px 10px;
                color:#fff;
                display:none;
            }
            .dialog-title {
                margin: 0;
                font-size: 1.25rem;
                font-weight: 600;
                color: #111827;
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
                margin-right: 1rem;
            }

            .dialog-close {
                background: none;
                border: none;
                font-size: 1.5rem;
                cursor: pointer;
                color: #6b7280;
                padding: 0;
                width: 2rem;
                height: 2rem;
                display: flex;
                align-items: center;
                justify-content: center;
                border-radius: 4px;
                transition: background-color 0.2s, color 0.2s;
                line-height: 1;
                flex-shrink: 0;
            }

            .dialog-close:hover {
                background-color: #e5e7eb;
                color: #111827;
            }

            .dialog-close:focus {
                outline: 2px solid #3b82f6;
                outline-offset: 2px;
            }

            /* Dialog body */
            .dialog-body {
                padding: 1.5rem;
                color: #374151;
                overflow: auto;
                flex: 1;
                min-height: 0;
            }

            /* Prevent body scroll when modal is open */
            body.dialog-open {
                overflow: hidden;
            }

            /* Responsive adjustments */
            @media (max-width: 640px) {
                .dialog {
                    padding: 0.5rem;
                }

                .dialog-content {
                    max-width: calc(100vw - 1rem);
                    max-height: calc(100vh - 1rem);
                    min-width: 0;
                    width: 100%;
                }

                .dialog-header {
                    padding: 0.75rem 1rem;
                }

                .dialog-body {
                    padding: 1rem;
                }
            }
        `;
        document.head.appendChild(style);
        this.stylesInjected = true;
    }
    showDialog(targetSel, isModal = false) {
        this.targetSel = targetSel;
        const dialogId = this.getDialogId(targetSel);
        let dialog = document.querySelector(`#${dialogId}`);
        // Create dialog if it doesn't exist
        if (!dialog) {
            dialog = this.createDialog(targetSel, dialogId, isModal);
            this.activeDialogs.set(dialogId, dialog);
        }
        // Show the dialog
        dialog.style.display = 'flex';
        if (isModal) {
            dialog.classList.add('modal');
            document.body.classList.add('dialog-open');
        }
        // Trigger reflow for animation
        dialog.offsetHeight;
        dialog.classList.add('show');
        // Update title based on p-title attribute
        this.updateTitleFromContent(targetSel);
        var errorId = this.getElementId(targetSel, 'error');
        this.plang.setErrorTarget('#' + errorId);
        this.plang.setDefaultTarget(targetSel);
        // Setup observer if not already watching
        if (!this.observers.has(targetSel)) {
            this.setupContentObserver(targetSel);
        }
        // Focus management
        this.manageFocus(dialog);
        return dialog;
    }
    getDialogId(targetSel) {
        // Remove # if present and append '-dialog'
        const cleanId = targetSel.replace(/^#/, '');
        return `${cleanId}-dialog`;
    }
    getElementId(targetSel, suffix) {
        const cleanId = targetSel.replace(/^#/, '');
        return `${cleanId}-${suffix}`;
    }
    createDialog(targetSel, dialogId, isModal) {
        const dialog = document.createElement('div');
        dialog.id = dialogId;
        dialog.className = 'dialog';
        dialog.setAttribute('role', 'dialog');
        dialog.setAttribute('aria-modal', isModal ? 'true' : 'false');
        // Create dialog structure
        const dialogContent = document.createElement('div');
        dialogContent.className = 'dialog-content';
        dialogContent.id = this.getElementId(targetSel, 'content');
        const dialogHeader = document.createElement('div');
        dialogHeader.className = 'dialog-header';
        dialogHeader.id = this.getElementId(targetSel, 'header');
        const dialogTitle = document.createElement('h2');
        dialogTitle.className = 'dialog-title';
        dialogTitle.id = this.getElementId(targetSel, 'title');
        dialogTitle.textContent = '';
        const closeBtn = document.createElement('button');
        closeBtn.className = 'dialog-close';
        closeBtn.id = this.getElementId(targetSel, 'close');
        closeBtn.innerHTML = '&times;';
        closeBtn.setAttribute('aria-label', 'Close dialog');
        // This is where the content goes - uses the original targetSel as id
        const dialogBody = document.createElement('div');
        dialogBody.className = 'dialog-body';
        dialogBody.id = targetSel.replace(/^#/, ''); // This gets the actual targetSel as id
        dialogHeader.appendChild(dialogTitle);
        dialogHeader.appendChild(closeBtn);
        dialogContent.appendChild(dialogHeader);
        dialogContent.appendChild(dialogBody);
        dialog.appendChild(dialogContent);
        document.body.appendChild(dialog);
        // Setup event listeners
        this.setupEventListeners(dialog, closeBtn, isModal);
        return dialog;
    }
    createErrorDiv(targetSel) {
        const dialogError = document.createElement('div');
        dialogError.className = 'dialog-error';
        dialogError.id = this.getElementId(targetSel, 'error');
        dialogError.textContent = '';
        return dialogError;
    }
    setupContentObserver(targetSel) {
        var errorId = this.getElementId(targetSel, 'error');
        if (errorId) {
            let errorElement = document.getElementById(errorId);
            if (!errorElement) {
                errorElement = this.createErrorDiv(targetSel);
            }
            var dialogContent = document.getElementById(this.getElementId(targetSel, 'content'));
            if (dialogContent) {
                dialogContent.appendChild(errorElement);
            }
            const observer = new MutationObserver((mutations) => {
                if (errorElement)
                    errorElement.style.display = 'block';
            });
            observer.observe(errorElement, {
                childList: true,
                characterData: true,
                subtree: true
            });
            this.observers.set(targetSel + "-error", observer);
        }
        const contentBody = document.querySelector(`#${targetSel.replace(/^#/, '')}`);
        if (!contentBody)
            return;
        const observer = new MutationObserver((mutations) => {
            // Check if any mutations affect elements with p-title attribute
            let shouldUpdate = false;
            for (const mutation of mutations) {
                if (mutation.type === 'childList') {
                    // Check added nodes for p-title
                    mutation.addedNodes.forEach(node => {
                        if (node.nodeType === Node.ELEMENT_NODE) {
                            const element = node;
                            if (element.hasAttribute('p-title') ||
                                element.querySelector('[p-title]')) {
                                shouldUpdate = true;
                            }
                        }
                    });
                }
                else if (mutation.type === 'attributes' && mutation.attributeName === 'p-title') {
                    shouldUpdate = true;
                }
            }
            if (shouldUpdate) {
                this.updateTitleFromContent(targetSel);
            }
        });
        observer.observe(contentBody, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ['p-title']
        });
        this.observers.set(targetSel, observer);
    }
    updateTitleFromContent(targetSel) {
        const contentBody = document.querySelector(`#${targetSel.replace(/^#/, '')}`);
        if (!contentBody)
            return;
        // Look for element with p-title attribute
        const elementWithTitle = contentBody.querySelector('[p-title]');
        if (elementWithTitle) {
            const titleValue = elementWithTitle.getAttribute('p-title');
            if (titleValue) {
                this.setDialogTitle(targetSel, titleValue);
            }
        }
    }
    setupEventListeners(dialog, closeBtn, isModal) {
        // Close button functionality
        closeBtn.addEventListener('click', () => {
            this.closeDialog(dialog, isModal);
        });
        let mouseDownTarget = null;
        dialog.addEventListener('mousedown', (e) => {
            mouseDownTarget = e.target;
        });
        // Close on backdrop click for modals
        if (isModal) {
            dialog.addEventListener('click', (e) => {
                if (e.target === dialog && mouseDownTarget === dialog) {
                    this.closeDialog(dialog, isModal);
                }
            });
        }
        // Close on Escape key
        const escapeHandler = (e) => {
            if (e.key === 'Escape' && dialog.classList.contains('show')) {
                this.closeDialog(dialog, isModal);
            }
        };
        document.addEventListener('keydown', escapeHandler);
        // Store the handler for cleanup if needed
        dialog.__escapeHandler = escapeHandler;
        dialog.__isModal = isModal;
    }
    closeDialog(dialog, isModal) {
        dialog.classList.remove('show');
        dialog.classList.remove('modal');
        // Check if this was a modal
        const wasModal = isModal ?? dialog.__isModal;
        if (wasModal) {
            document.body.classList.remove('dialog-open');
        }
        if (this.targetSel) {
            var errorId = this.getElementId(this.targetSel, 'error');
            var errorElement = document.getElementById(errorId);
            if (errorElement) {
                errorElement.style.display = 'none';
            }
        }
        this.plang.removeErrorTarget();
        this.plang.removeDefaultTarget();
        // Remove after animation
        setTimeout(() => {
            if (!dialog.classList.contains('show')) {
                dialog.style.display = 'none';
            }
        }, 300);
    }
    manageFocus(dialog) {
        const focusableElements = dialog.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
        if (focusableElements.length > 0) {
            focusableElements[0].focus();
        }
    }
    // Utility method to update dialog content (targets the body with id=targetSel)
    setDialogContent(targetSel, content) {
        const body = document.querySelector(`#${targetSel.replace(/^#/, '')}`);
        if (body) {
            if (typeof content === 'string') {
                body.innerHTML = content;
            }
            else {
                body.innerHTML = '';
                body.appendChild(content);
            }
            // Update title after content change
            this.updateTitleFromContent(targetSel);
        }
    }
    // Utility method to set dialog title
    setDialogTitle(targetSel, title) {
        const titleId = this.getElementId(targetSel, 'title');
        const titleElement = document.querySelector(`#${titleId}`);
        if (titleElement) {
            titleElement.textContent = title;
        }
    }
    // Manually close a dialog by targetSel
    close() {
        const dialogId = this.getDialogId(this.targetSel);
        const dialog = document.querySelector(`#${dialogId}`);
        if (dialog) {
            const isModal = dialog.__isModal;
            this.closeDialog(dialog, isModal);
        }
    }
    // Cleanup method (useful for SPA scenarios)
    destroy(targetSel) {
        const dialogId = this.getDialogId(targetSel);
        const dialog = document.querySelector(`#${dialogId}`);
        if (dialog) {
            // Remove escape handler
            const escapeHandler = dialog.__escapeHandler;
            const isModal = dialog.__isModal;
            if (escapeHandler) {
                document.removeEventListener('keydown', escapeHandler);
            }
            if (isModal) {
                document.body.classList.remove('dialog-open');
            }
            dialog.remove();
            this.activeDialogs.delete(dialogId);
        }
        // Disconnect and remove observer
        const observer = this.observers.get(targetSel);
        if (observer) {
            observer.disconnect();
            this.observers.delete(targetSel);
        }
    }
}
//# sourceMappingURL=DialogManager.js.map