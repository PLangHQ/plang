import type { FrameworkAdapter, ActionOverrideHandler } from "./ActionOverrides.js";

export class TailwindAdapter implements FrameworkAdapter {
  getName(): string {
    return 'tailwind';
  }
  
  getOverrides(): Record<string, ActionOverrideHandler> {
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