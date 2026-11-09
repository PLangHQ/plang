export class BootstrapAdapter {
    getName() {
        return 'bootstrap';
    }
    getOverrides() {
        return {
            show: (nodes) => {
                nodes.forEach(el => el.classList.remove('d-none'));
            },
            hide: (nodes) => {
                nodes.forEach(el => el.classList.add('d-none'));
            }
        };
    }
}
//# sourceMappingURL=BootstrapAdapter.js.map