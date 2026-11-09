import { errorReporter } from "./ErrorReporter.js";
import { t as defaultT } from "../i18n/index.js";
export class PollRequest {
    constructor(url = "/?plang.poll=1", options = {}, tr = defaultT) {
        this.controller = null;
        this.url = url;
        this.onMessage = options.onMessage ?? (() => { });
        this.reconnectDelay = options.reconnectDelay ?? 10_000;
        this.tr = tr;
        void this.connect(false);
    }
    close() {
        this.controller?.abort();
    }
    async connect(refresh) {
        this.controller = new AbortController();
        let url = this.url;
        if (refresh)
            url += (url.includes("?") ? "&" : "?") + "refresh=1";
        if (!refresh && typeof visitSourcesId !== "undefined") {
            url += (url.includes("?") ? "&" : "?") + "visitSourcesId=" + encodeURIComponent(String(visitSourcesId));
        }
        if (!refresh && typeof visitSource !== "undefined") {
            url += (url.includes("?") ? "&" : "?") + "visitSource=" + encodeURIComponent(String(visitSource));
        }
        if (!refresh && typeof teacherUrlCode !== "undefined") {
            url += (url.includes("?") ? "&" : "?") + "teacherUrlCode=" + encodeURIComponent(String(teacherUrlCode));
        }
        // Fyrsta snerting. Fyrsta beidnin er venjuleg vafraflutningur (GET) og ber ekkert audkenni,
        // svo Visit sleppir visitsIdentity-rodinni og LENDINGARSIDAN TYNIST. Uppruninn skilar ser
        // (hann bidur i visitSource her ad ofan) en ekkert segir hvad gesturinn kom ad skoda, svo
        // flaedid i "Uppruni heimsokna" stod tomt - strik. Vid sendum thvi slodina med fyrsta polli
        // og serverinn skrifar rodina thegar audkennid er ordid til.
        if (!refresh) {
            // Which rung of the key store ladder this browser landed on. The server writes it
            // onto the identity row, so we can count how many PEOPLE are degraded rather than
            // how many page loads: crawlers never link a user, and they are 95% of identities.
            const tier = window.plang?.keyStoreTier;
            if (tier && tier !== "indexeddb") {
                url += (url.includes("?") ? "&" : "?") + "keyStore=" + encodeURIComponent(tier);
            }
            url += (url.includes("?") ? "&" : "?") + "landingPath=" + encodeURIComponent(location.pathname);
            const leitarord = new URLSearchParams(location.search).get("q");
            if (leitarord)
                url += "&landingQuery=" + encodeURIComponent(leitarord);
            // Hvadan gesturinn kom. Server-hausinn dugar ekki: rodin er skrifud vid POLLID en ekki
            // fyrsta GET (audkennid er ekki til tha), og pollid er same-origin svo Referer-hausinn
            // er okkar eigid len. document.referrer heldur hins vegar ytra leninu, sem er eina
            // leidin til ad sja kennara sem limir beina bokarslod i Canvas i stad /r/ hlekksins.
            // Adeins YTRI referrer er sendur; innan-sidu flakk segir ekkert um uppruna.
            const referrer = document.referrer;
            if (referrer && !referrer.startsWith(location.origin)) {
                url += "&landingReferrer=" + encodeURIComponent(referrer);
            }
        }
        try {
            const res = await window.plang.fetch(url, { signal: this.controller.signal });
            // fetch() legitimately returns nothing on three paths: a download response, a
            // response with no body, and the end of a stream it consumed itself. Reading
            // res.body without checking res threw a TypeError on all of them. The guard
            // below anticipated a missing body but not a missing response.
            // Nothing to read is not a failure, it means this poll cycle is over, so return
            // and let the finally block reconnect on its timer.
            const body = res?.body;
            if (!body)
                return;
            const reader = body.pipeThrough(new TextDecoderStream()).getReader();
            for await (const line of this.streamToLines(reader)) {
                window.plang.onPoll(line);
                this.onMessage(line, {});
            }
        }
        catch (err) {
            // This is the failure that hid itself for a year. The poll is the only traffic
            // on a freshly loaded page, so swallowing it here meant a browser that cannot
            // start the poll sat on a spinner and told us nothing.
            console.warn(this.tr("plangConnectFailed"), err);
            errorReporter.reportCaught("poll", err);
        }
        finally {
            setTimeout(() => void this.connect(true), this.reconnectDelay);
        }
    }
    async *streamToLines(reader) {
        let buffer = "";
        while (true) {
            const { value, done } = await reader.read();
            if (done)
                break;
            buffer += value ?? "";
            const parts = buffer.split("\n");
            buffer = parts.pop() ?? "";
            for (const raw of parts) {
                const line = raw.trim();
                if (line)
                    yield line;
            }
        }
    }
}
//# sourceMappingURL=PollRequest.js.map