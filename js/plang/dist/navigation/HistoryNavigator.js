import { t as defaultT } from "../i18n/index.js";
export class HistoryNavigator {
    constructor(plang, tr = defaultT) {
        this.elementMap = new Map();
        this.plang = plang;
        this.tr = tr;
        addEventListener("popstate", (e) => {
            const s = e.state;
            if (!s) {
                console.warn('no state found');
                return;
            }
            if (s.options.sourceElement) {
                var element = this.elementMap.get(s.options.sourceElement.toString());
                if (element)
                    s.options.sourceElement = element;
            }
            if (s.options.body) {
                var data = this.elementMap.get(s.options.body.toString());
                if (data)
                    s.options.body = data;
            }
            // popstate says how we got here, scroll says where to. They were one field before, so
            // "did this load come from Back?" was only true when the entry happened to carry a
            // scroll position - and an entry only gets one when it is left by a click, never when
            // it is left by Back. A page reached twice by Back therefore pushed itself onto the
            // history again on the second visit, and Back could never walk past it.
            s.options.popstate = true;
            s.options.scroll = s.scroll ?? [0, 0];
            const load = this.plang.fetch(s.url, s.options);
            load
                .then(() => {
                const coords = s.scroll ?? [0, 0];
                //scrollTo(coords[0], coords[1]);
            })
                .catch((err) => {
                console.warn(this.tr("navPopstateFailed"), err);
                this.plang.reportCaught("navPopstate", err);
            });
        });
        const state = { url: document.location.href, options: { method: 'GET' } };
        if (!history.state) {
            this.setState(state, true);
        }
    }
    navigate(url, options) {
        this.saveScrollPosition();
        const newState = { url, options, scroll: [0, 0] };
        const load = this.plang.fetch(url, options);
        load
            .catch((error) => {
            console.log(this.tr("navRequestFailed"), error);
            this.plang.reportCaught("navRequest", error);
        });
        return load;
    }
    saveScrollPosition() {
        const st = history.state;
        if (st) {
            if (st.options.sourceElement instanceof Element) {
                st.options.sourceElement = this.swapSourceElement(st.options.sourceElement);
            }
            if (st.options.body) {
                st.options.body = this.swapSourceElement(st.options.body);
            }
            history.replaceState({ ...st, scroll: [scrollX, scrollY] }, "", location.href);
        }
    }
    setState(newState, replace = false) {
        if (newState.options.sourceElement instanceof Element) {
            newState.options.sourceElement = this.swapSourceElement(newState.options.sourceElement);
        }
        if (newState.options.body) {
            newState.options.body = this.swapSourceElement(newState.options.body);
        }
        if (replace) {
            history.replaceState(newState, "", newState.url);
        }
        else if (newState.options.method === "GET") {
            history.pushState(newState, "", newState.url);
        }
    }
    swapSourceElement(sourceElement) {
        var id = sourceElement.id;
        if (!id)
            id = crypto.randomUUID();
        this.elementMap.set(id, sourceElement);
        return id.toString();
    }
}
//# sourceMappingURL=HistoryNavigator.js.map