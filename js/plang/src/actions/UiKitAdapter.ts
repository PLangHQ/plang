import type { FrameworkAdapter, ActionOverrideHandler } from "./ActionOverrides.js";

export class UiKitAdapter implements FrameworkAdapter {
  getName(): string {
    return 'uikit';
  }
  
  getOverrides(): Record<string, ActionOverrideHandler> {
    return {
      show: (nodes, message, ctx) => {
        nodes.forEach(el => {
            el.style.display = '';
            el.classList.remove('uk-hidden')
        });
      },
      
      hide: (nodes, message, ctx) => {
        nodes.forEach(el => el.classList.add('uk-hidden'));
      },

    };
  }
}