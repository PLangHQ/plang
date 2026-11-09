export class UiKitAdapter {
    getName() {
        return 'uikit';
    }
    getOverrides() {
        return {
            show: (nodes, message, ctx) => {
                nodes.forEach(el => {
                    el.style.display = '';
                    el.classList.remove('uk-hidden');
                });
            },
            hide: (nodes, message, ctx) => {
                nodes.forEach(el => el.classList.add('uk-hidden'));
            },
        };
    }
}
//# sourceMappingURL=UiKitAdapter.js.map