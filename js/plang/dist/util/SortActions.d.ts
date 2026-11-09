export declare class ActionSorter {
    private static readonly priorityOrder;
    /**
     * Sorts actions array with specific priority:
     * 1. showDialog, showModal
     * 2. replace, replaceSelf, append, prepend, appendOrReplace, prependOrReplace
     * 3. All other actions
     *
     * @param actions - Array of action strings to sort
     * @returns Sorted array of actions
     */
    static sort(actions: string[] | undefined): string[];
    /**
     * Sorts actions array in place
     * @param actions - Array of action strings to sort in place
     */
    static sortInPlace(actions: string[]): void;
    /**
     * Gets the priority level of an action
     * @param action - Action string
     * @returns Priority level (lower number = higher priority)
     */
    static getPriority(action: string): number;
}
