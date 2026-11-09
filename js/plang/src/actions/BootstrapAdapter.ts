import type { FrameworkAdapter, ActionOverrideHandler } from "./ActionOverrides.js";

export class BootstrapAdapter implements FrameworkAdapter {
  getName(): string {
    return 'bootstrap';
  }
  
  getOverrides(): Record<string, ActionOverrideHandler> {
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