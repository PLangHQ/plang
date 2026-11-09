/*

type PlangOutput =
  | { type: "error"; data?: any; message?: string; key?: string; properties?: Record<string, any> }
  | { type: "js"; data: { methodName: string; parameters: any } }
  | { type: string; data?: any; properties?: Record<string, any> }
  | string; // "ping"

class Old {


  async onMessage(plangOutput: PlangOutput, { sourceElement = null as Element | null } = {}) {
    if (plangOutput === "ping") return;

    let fn: any = null;
    let params: any = null;

    const props = (plangOutput as any)?.properties ?? {};
    if ((plangOutput as any)?.type !== "error" && (plangOutput as any)?.type !== "javascriptFunction" && props["data-plang-response"]) {
      fn = (window as any)[props["data-plang-response"]] ?? (this as any)[(plangOutput as any).type];
      params = props["data-plang-response-params"]
        ? this.safeParseParams(plangOutput, props["data-plang-response-params"])
        : plangOutput;
    } else if ((plangOutput as any)?.type === "error") {
      await this.error(plangOutput, { sourceElement });
      return;
    } else if ((plangOutput as any)?.type === "js") {
      fn = (window as any)[(plangOutput as any).data.methodName];
      params = (plangOutput as any).data.parameters;
    } else {
      fn = (this as any)[(plangOutput as any).type];
      params = plangOutput;
    }

    if (typeof fn === "function") {
      await fn.call(this, params);
    } else {
      console.error(this.tr("fnNotFound", { fn: JSON.stringify(plangOutput) }));
      await this.error(plangOutput as any, { sourceElement });
      throw new Error(this.tr("fnNotFound", { fn: JSON.stringify(plangOutput) }));
    }
  }

  async javascriptFunction(obj: any) {
    const { methodName, parameters } = obj.data ?? {};
    const f = (window as any)[methodName];
    if (typeof f === "function") f(parameters);
    else console.error(this.tr("windowMethodMissing", { name: String(methodName) }), parameters);
  }

  async domRemove(obj: any) {
    const list = obj.data ?? [];
    list.forEach((item: any) => {
      document.querySelectorAll(item.selector).forEach((n) => n.remove());
    });
  }

  async domInstruction(obj: any) {
    const list = obj.data ?? [];

    const DomMemberKind = { Property: 0, Attribute: 1, Style: 2, Event: 3 } as const;
    const attrToProp: Record<string, "checked" | "selected" | "value"> = { checked: "checked", selected: "selected", value: "value" };

    for (const item of list) {
      const { selector, member, value, kind } = item;
      const nodes = document.querySelectorAll<HTMLElement>(selector);
      if (!nodes.length) continue;

      switch (kind) {
        case DomMemberKind.Property:
          nodes.forEach((n) => ((n as any)[member] = value));
          break;
        case DomMemberKind.Attribute:
          nodes.forEach((n) => {
            if (value == null) {
              n.removeAttribute(member);
              if (attrToProp[member]) (n as any)[attrToProp[member]] = attrToProp[member] === "value" ? "" : false;
            } else {
              n.setAttribute(member, value);
              if (attrToProp[member]) (n as any)[attrToProp[member]] = member === "value" ? value : !!value;
            }
          });
          break;
        case DomMemberKind.Style:
          nodes.forEach((n) => ((n.style as any)[member] = value));
          break;
        case DomMemberKind.Event: {
          const handler =
            typeof value === "function"
              ? value
              : typeof value === "string"
                ? (window as any)[value] || new Function("event", value)
                : null;
          if (!handler) break;
          nodes.forEach((n) => n.addEventListener(member, handler as any));
          break;
        }
      }
    }
  }

  async notification(plangOutput: any) {
    const n = plangOutput.data ?? {};
    window.UIkit?.notification?.({
      message: n.message,
      status: n.status,
      pos: n.position,
      timeout: n.timeout,
    });
  }


  async html(plangOutput: any) {
    const { html, scripts, styles, elementId } = this.extractHtmlAndScripts(plangOutput.data);
    let action: "innerHTML" | "outerHTML" | "set" | "overwrite" = "innerHTML";
    let cssSelector = "#main";
    let id = crypto.randomUUID();
    let reRender = true;

    if (plangOutput.properties) {
      action = plangOutput.properties["action"] ?? "innerHTML";
      if (action === "overwrite") action = "outerHTML";
      if (action === "set") action = "innerHTML";
      cssSelector = plangOutput.properties["cssSelector"] ?? "#main";
      id = plangOutput.properties["id"] ?? "/";
      reRender = plangOutput.properties["reRender"] ?? true;
    }

    if (!reRender) {
      if (elementId) {
        const elementExists = document.getElementById(elementId.id);
        if (elementExists) return;
      }
    }

    this.insertStyles(styles);

    document.querySelectorAll<HTMLElement>(cssSelector).forEach((container) => {
      container.classList.add("uk-animation-fade");
      const anyContainer = container as any;
      if (typeof anyContainer[action] === "function") anyContainer[action](...Array.from(html.childNodes));
      else (anyContainer[action] = html.innerHTML);

      container.addEventListener(
        "animationend",
        () => {
          container.classList.remove("uk-animation-fade");
          container.classList.add("uk-animation-slide-left-small");
          requestAnimationFrame(() => container.classList.remove("uk-animation-slide-left-small"));
        },
        { once: true }
      );
    });

    await this.insertScripts(scripts, id);
    if (cssSelector === "#main") scrollTo(0, 0);
  }

  
  async warning(obj: any, params?: any) {
    await this.error(obj, { sourceElement: null });
  }

  async error(obj: any, { sourceElement = null as Element | null } = {}) {
    if (obj.type === "html") {
      await this.onMessage(obj, { sourceElement });
      return;
    }

    const error = obj.data ?? obj;

    if (sourceElement && error.type === "UserInputError") {
      const method = (sourceElement as HTMLElement).getAttribute("data-plang-error");
      if (method) {
        const fn = (window as any)[method];
        if (typeof fn === "function") {
          fn.call(this, error);
          return;
        }
      }
    }

    const main = document.querySelector<HTMLElement>("#main");

    if (error.key !== "UserInputError") {
      const message =
        obj.message ??
        `<div class="uk-text-center uk-padding">
          ${this.tr("unexpectedError")}
        </div>`;
      window.UIkit?.notification?.({
        message,
        status: "danger",
        pos: "top-center",
        timeout: 5000,
      });
      return;
    }

    if (error.message) {
      window.UIkit?.notification?.({
        message: `<div class="uk-text-center uk-padding">${error.message}</div>`,
        status: "danger",
        pos: "top-center",
        timeout: 5000,
      });
    } else if (obj.type === "html" && main) {
      main.innerHTML = obj.data;
    } else if (main) {
      main.innerHTML += "<pre>" + JSON.stringify(obj, null, 4) + "</pre>";
    }
  }

  async loading(obj: any) {
    const loading = obj.data ?? {};
    if (loading.start) {
      document.querySelector(loading.targetElement)?.append(loading.html);
    } else {
      document.querySelector(loading.id)?.remove();
    }
  }

  async paymentRequest(obj: any) {
    const pr = obj.data ?? {};
    document.querySelector(pr.targetElement)?.append(pr.html);
  }


  
  private safeParseParams(obj: any, raw: string | undefined) {
    if (!raw) return { ...obj };
    try {
      // eslint-disable-next-line no-new-func
      const parsed = Function('"use strict";return (' + raw + ")")();
      return { ...obj, ...parsed };
    } catch {
      console.error(this.tr("invalidParams"), raw);
      return {};
    }
  }
}


  */