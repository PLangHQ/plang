export class TailwindAdapter {
    getName() {
        return 'tailwind';
    }
    getOverrides() {
        return {
            show: (nodes) => {
                nodes.forEach(el => el.classList.remove('hidden'));
            },
            hide: (nodes) => {
                nodes.forEach(el => el.classList.add('hidden'));
            }
        };
    }
}
//# sourceMappingURL=TailwindAdapter.js.map