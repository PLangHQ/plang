namespace app.module.browser.code;

/// <summary>
/// Video redirection, the page's half: a script every page gets before its own (DevTools, when the
/// page starts). A video a page plays through MediaSource in a codec the host decodes (VP9, H.264)
/// isn't decoded here: the page's video buffer is a stand-in — its appends go to plang
/// (<c>plangMedia</c>, the bytes as base64), which parses them and answers with what is buffered —
/// and Chromium plays the sound alone (its clock runs the page as before). The video element is
/// painted in a key colour; where that colour is on the screen, the host draws the video, decoded
/// by Windows. The element's place, time and state go to plang as they change.
/// Messages to plang: {op:"add",buffer,mime} {op:"append",buffer,id,data} {op:"remove",buffer,id,start,end}
/// {op:"abort",buffer} {op:"offset",buffer,value} {op:"state",buffer,rect,time,paused,rate,seeking}
/// {op:"gone",buffer}. From plang: __plangMedia.reply({id,buffered:[[s,e],…]}).
/// </summary>
internal static class Media
{
    /// <summary>The binding the script speaks through: only a video's news goes through it.</summary>
    internal const string Binding = "plangMedia";

    /// <summary>The key colour: where it is on the screen, the host draws the video.</summary>
    internal const uint Key = 0xFFFE01FD;   // BGRA as a little-endian word: r 254, g 1, b 253

    internal const string Script = """
(() => {
  // the binding may arrive after this script runs (DevTools adds it as the page starts): looked up when used
  if (!window.MediaSource || window.__plangMedia) return;
  const say = s => window.plangMedia(s);
  const KEY = 'rgb(254, 1, 253)';
  const played = /video\/webm;\s*codecs="?(vp09|vp9)|video\/mp4;\s*codecs="?avc1/i;   // what the host decodes
  const refused = /av01|hev1|hvc1/i;                                   // what it doesn't: the page picks another
  const post = m => { try { say(JSON.stringify(m)); } catch (e) {} };
  const waiting = new Map();
  let ids = 0, buffers = 0;
  const doc = Math.random().toString(36).slice(2);   // this document's buffers aren't a navigated-from one's
  Object.defineProperty(window, '__plangMedia', { value: { reply: m => { const w = waiting.get(m.id); if (w) { waiting.delete(m.id); w(m); } } } });

  // asking: the page picks a codec the host can show
  for (const MS of [window.MediaSource, window.ManagedMediaSource]) {
    if (!MS) continue;
    const supported = MS.isTypeSupported.bind(MS);
    MS.isTypeSupported = t => /^video\//i.test(t) && refused.test(t) ? false : supported(t);
  }

  const urls = new WeakMap();
  const createURL = URL.createObjectURL;
  URL.createObjectURL = function (o) { const u = createURL.apply(this, arguments); if (o instanceof MediaSource) urls.set(o, u); return u; };

  const base64 = bytes => {
    let s = '';
    for (let i = 0; i < bytes.length; i += 0x8000) s += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
    return btoa(s);
  };
  const ranges = list => ({ length: list.length, start: i => list[i][0], end: i => list[i][1] });

  // the stand-in: what a SourceBuffer is to the page, its bytes going to plang
  const add = MediaSource.prototype.addSourceBuffer;
  MediaSource.prototype.addSourceBuffer = function (mime) {
    if (!played.test(mime)) return add.call(this, mime);
    const ms = this, buffer = doc + '-' + (++buffers);
    const listeners = {};
    let updating = false, offset = 0, buffered = [], element = null, lastState = '', lastSent = 0;
    let real = null, decided = false;   // a video without sound isn't redirected: Chromium has nothing else to play it by
    const sb = Object.create(SourceBuffer.prototype);
    // the first append decides: no other (real) buffer in this source — a video alone — and this one
    // becomes a real buffer, everything the page does passed to it
    const decide = () => {
      if (decided) return;
      decided = true;
      if (ms.sourceBuffers.length > 0) return;
      real = add.call(ms, mime);
      for (const t of ['updatestart', 'update', 'updateend', 'error', 'abort']) real.addEventListener(t, () => fire(t));
      if (offset) real.timestampOffset = offset;
      post({ op: 'gone', buffer });
    };
    const fire = type => {
      const e = new Event(type);
      for (const f of (listeners[type] || []).slice()) try { f.call(sb, e); } catch (x) { setTimeout(() => { throw x; }); }
      const on = sb['on' + type];
      if (typeof on === 'function') try { on.call(sb, e); } catch (x) { setTimeout(() => { throw x; }); }
    };
    const ask = (m, done) => {
      const id = ++ids;
      m.id = id; m.buffer = buffer;
      updating = true;
      fire('updatestart');
      waiting.set(id, r => { buffered = r.buffered || buffered; updating = false; fire('update'); fire('updateend'); if (done) done(r); });
      post(m);
    };
    const value = (name, v) => Object.defineProperty(sb, name, { value: v, writable: true, configurable: true });
    const prop = (name, get, set) => Object.defineProperty(sb, name, { get, set, configurable: true });
    value('addEventListener', (t, f) => { if (typeof f === 'function') (listeners[t] = listeners[t] || []).push(f); });
    value('removeEventListener', (t, f) => { const l = listeners[t]; if (l) { const i = l.indexOf(f); if (i >= 0) l.splice(i, 1); } });
    value('dispatchEvent', e => { fire(e.type); return true; });
    for (const t of ['updatestart', 'update', 'updateend', 'error', 'abort']) value('on' + t, null);
    prop('updating', () => real ? real.updating : updating);
    prop('buffered', () => real ? real.buffered : ranges(buffered));
    prop('timestampOffset', () => real ? real.timestampOffset : offset, v => {
      if (real) { real.timestampOffset = v; return; }
      offset = +v; post({ op: 'offset', buffer, value: offset });
    });
    let windowStart = 0, windowEnd = Infinity;
    prop('appendWindowStart', () => real ? real.appendWindowStart : windowStart, v => { if (real) real.appendWindowStart = v; else windowStart = +v; });
    prop('appendWindowEnd', () => real ? real.appendWindowEnd : windowEnd, v => { if (real) real.appendWindowEnd = v; else windowEnd = +v; });
    prop('mode', () => real ? real.mode : 'segments', v => { if (real) real.mode = v; });
    value('appendBuffer', data => {
      decide();
      if (real) return real.appendBuffer(data);
      if (updating) throw new DOMException('updating', 'InvalidStateError');
      const bytes = data instanceof ArrayBuffer ? new Uint8Array(data) : new Uint8Array(data.buffer, data.byteOffset, data.byteLength);
      ask({ op: 'append', data: base64(bytes) });
    });
    value('remove', (start, end) => {
      if (real) return real.remove(start, end);
      if (updating) throw new DOMException('updating', 'InvalidStateError');
      ask({ op: 'remove', start: +start, end: +end });
    });
    value('abort', () => { if (real) return real.abort(); post({ op: 'abort', buffer }); updating = false; });
    value('changeType', t => { if (real) return real.changeType(t); post({ op: 'add', buffer, mime: t }); });
    post({ op: 'add', buffer, mime });

    // the element this source plays in: its place, time and state, as they change
    const find = () => {
      const url = urls.get(ms);
      for (const v of document.querySelectorAll('video')) if (url && (v.src === url || v.currentSrc === url)) return v;
      return null;
    };
    const tell = () => {
      if (real || !decided) return;
      if (!element || !element.isConnected) element = find();
      if (!element) return;
      element.style.setProperty('background-color', KEY, 'important');
      const r = element.getBoundingClientRect(), d = devicePixelRatio;
      const m = { op: 'state', buffer, rect: [r.x * d, r.y * d, r.width * d, r.height * d].map(Math.round),
        time: element.currentTime, paused: element.paused, rate: element.playbackRate, seeking: element.seeking,
        shown: document.visibilityState === 'visible' };
      // when anything but the clock changed; the clock anyway once a second (the host keeps its own between)
      const same = JSON.stringify([m.rect, m.paused, m.rate, m.seeking, m.shown]), now = performance.now();
      if (same === lastState && now - lastSent < 1000) return;
      lastState = same; lastSent = now;
      post(m);
    };
    const tick = setInterval(tell, 250);
    for (const t of ['play', 'playing', 'pause', 'seeking', 'seeked', 'ratechange', 'waiting', 'emptied'])
      document.addEventListener(t, e => { if (e.target === element) { lastState = ''; tell(); } }, true);
    addEventListener('resize', () => { lastState = ''; tell(); });
    addEventListener('scroll', () => { lastState = ''; tell(); }, true);
    ms.addEventListener('sourceclose', () => { clearInterval(tick); post({ op: 'gone', buffer }); });
    return sb;
  };
})();
""";
}
