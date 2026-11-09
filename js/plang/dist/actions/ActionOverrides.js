import { UiKitAdapter } from "./UiKitAdapter.js";
import { BootstrapAdapter } from "./BootstrapAdapter.js";
import { TailwindAdapter } from "./TailwindAdapter.js";
// Registry for built-in adapters
export class FrameworkRegistry {
    static register(adapter) {
        this.adapters.set(adapter.getName(), adapter);
    }
    static get(name) {
        return this.adapters.get(name);
    }
    static detectFramework() {
        if (window.UIkit)
            return 'uikit';
        if (window.bootstrap)
            return 'bootstrap';
        return null;
    }
}
FrameworkRegistry.adapters = new Map();
// Registered here rather than in a `static {}` block. Static initialization blocks are ES2022 and
// Safari only parses them from 16.4, so on 16.1 the whole module graph failed to parse and the
// client never booted: no identity, no cart, nothing. A parse error cannot be caught at runtime,
// which is why it showed up only in the pre-boot trap in the layout.
FrameworkRegistry.register(new UiKitAdapter());
FrameworkRegistry.register(new BootstrapAdapter());
FrameworkRegistry.register(new TailwindAdapter());
//# sourceMappingURL=ActionOverrides.js.map