export class ActionSorter {
    /**
     * Sorts actions array with specific priority:
     * 1. showDialog, showModal
     * 2. replace, replaceSelf, append, prepend, appendOrReplace, prependOrReplace
     * 3. All other actions
     *
     * @param actions - Array of action strings to sort
     * @returns Sorted array of actions
     */
    static sort(actions) {
        if (!actions)
            return ['replace'];
        return [...actions].sort((a, b) => {
            const priorityA = this.priorityOrder[a] || 999;
            const priorityB = this.priorityOrder[b] || 999;
            // If same priority, maintain original order
            if (priorityA === priorityB) {
                return 0;
            }
            return priorityA - priorityB;
        });
    }
    /**
     * Sorts actions array in place
     * @param actions - Array of action strings to sort in place
     */
    static sortInPlace(actions) {
        actions.sort((a, b) => {
            const priorityA = this.priorityOrder[a] || 999;
            const priorityB = this.priorityOrder[b] || 999;
            if (priorityA === priorityB) {
                return 0;
            }
            return priorityA - priorityB;
        });
    }
    /**
     * Gets the priority level of an action
     * @param action - Action string
     * @returns Priority level (lower number = higher priority)
     */
    static getPriority(action) {
        return this.priorityOrder[action] || 999;
    }
}
ActionSorter.priorityOrder = {
    // Tier 1: Dialogs/Modals first
    'showDialog': 1,
    'showModal': 1,
    // Tier 2: Content manipulation
    'replace': 2,
    'replaceSelf': 2,
    'append': 2,
    'prepend': 2,
    'appendOrReplace': 2,
    'prependOrReplace': 2,
    // Tier 3: Everything else
    'scrollToTop': 3,
    'scrollIntoView': 3,
    'focus': 3,
    'highlight': 3,
    'show': 3,
    'hide': 3,
    'hideDialog': 3,
    'hideModal': 3,
    'notify': 3,
    'alert': 3,
    'vibrate': 3,
    'navigate': 3,
    'replaceState': 3,
    'reload': 3,
    'open': 3,
    'close': 3
};
//# sourceMappingURL=SortActions.js.map