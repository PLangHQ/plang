// A page's video, played by the host instead of Chromium (video pass-through) — any site, through the standard Media
// Source API, nothing site-specific. Added to every page before its own scripts, with window.__plangCodecs = what the
// host decodes (e.g. ["av01", "avc1"]). Nothing changes when that is empty, or for anything but Media Source video in
// MP4 with a codec the host plays: WebM, DRM, canvas, a plain <video src> are Chromium's, drawn and sent as pixels.
//
// For a played video, its SourceBuffer is a stand-in: it takes the player's chunks and passes them to plang
// (plangVideo), never to Chromium, which plays the audio only — so the element's clock, seeking and buffering stay
// Chromium's own. The stand-in reports the audio's buffered ranges and fires the events a player waits for; the
// element reports the video's size and frame counts. The element's place is painted the key colour, which the host
// replaces with the video where the page shows it (the player's controls, captions and ads are drawn over it).
(() => {
  const codecs = window.__plangCodecs || [];
  if (!codecs.length || !window.MediaSource) return;
  // plangVideo is DevTools' binding; it may come after this script: what is said before it is there waits (a stream's
  // init segment must not be lost), in order
  const waiting = [];
  const say = message => {
    waiting.push(JSON.stringify(message));
    if (typeof window.plangVideo !== 'function') { if (waiting.length === 1) setTimeout(() => say.flush(), 50); return; }
    say.flush();
  };
  say.flush = () => {
    if (typeof window.plangVideo !== 'function') { setTimeout(() => say.flush(), 50); return; }
    while (waiting.length) window.plangVideo(waiting.shift());
  };
  const KEY = 'rgb(1, 2, 3)';
  // a new page in the window (a reload, another address): the last page's videos are over — it never said so
  if (window === window.top) say({ video: 'page' });

  // what the host plays: video in MP4 with one of its codecs (the codec string's first part: av01.0.08M.08 → av01)
  const played = type => {
    const m = /^video\/mp4\s*;\s*codecs\s*=\s*"?([a-z0-9]+)/i.exec(type || '');
    return !!m && codecs.includes(m[1].toLowerCase());
  };
  // what can be played is answered as Chromium answers it: a player picks by its own preference, and whatever it picks
  // that the host plays (MP4 in one of its codecs) is passed through; the rest Chromium plays, as pixels

  // what the stream says about itself, read from its MP4 boxes: the size (tkhd) and, from the first media segment, the
  // frame rate (a sample's duration over the track's timescale) — no site's own numbers
  const box = (bytes, name, from = 0, to = bytes.length) => {
    for (let at = from; at + 8 <= to;) {
      const size = (bytes[at] << 24 | bytes[at + 1] << 16 | bytes[at + 2] << 8 | bytes[at + 3]) >>> 0;
      const type = String.fromCharCode(bytes[at + 4], bytes[at + 5], bytes[at + 6], bytes[at + 7]);
      if (size < 8 || at + size > to) return null;
      if (type === name) return { start: at + 8, end: at + size };
      at += size;
    }
    return null;
  };
  const u32 = (b, at) => (b[at] << 24 | b[at + 1] << 16 | b[at + 2] << 8 | b[at + 3]) >>> 0;
  const path = (bytes, names) => names.reduce((found, name) => found && box(bytes, name, found.start, found.end), { start: 0, end: bytes.length });
  const learn = (stream, bytes) => {
    const tkhd = path(bytes, ['moov', 'trak', 'tkhd']);
    if (tkhd) { stream.w = u32(bytes, tkhd.end - 8) >>> 16; stream.h = u32(bytes, tkhd.end - 4) >>> 16; }
    const mdhd = path(bytes, ['moov', 'trak', 'mdia', 'mdhd']);
    if (mdhd) stream.timescale = u32(bytes, mdhd.start + (bytes[mdhd.start] === 1 ? 20 : 12));
    const trex = path(bytes, ['moov', 'mvex', 'trex']);
    if (trex) stream.duration = u32(bytes, trex.start + 12);
    const tfhd = path(bytes, ['moof', 'traf', 'tfhd']);
    if (tfhd && (u32(bytes, tfhd.start) & 0x08)) stream.duration = u32(bytes, tfhd.start + 8 + ((u32(bytes, tfhd.start) & 0x01) ? 8 : 0) + ((u32(bytes, tfhd.start) & 0x02) ? 4 : 0));
    const trun = path(bytes, ['moof', 'traf', 'trun']);
    if (trun && (u32(bytes, trun.start) & 0x100)) {
      const flags = u32(bytes, trun.start);
      stream.duration = u32(bytes, trun.start + 8 + (flags & 0x01 ? 4 : 0) + (flags & 0x04 ? 4 : 0));
    }
  };
  const rate = s => s.timescale && s.duration ? s.timescale / s.duration : 0;

  let streams = 0;
  const style = document.createElement('style');
  style.textContent = `video[data-plang-video]{background:${KEY}!important}`;

  class StandIn extends EventTarget {
    constructor(source, type) {
      super();
      this.source = source; this.type = type; this.id = ++streams;
      this.updating = false; this.mode = 'segments'; this.timestampOffset = 0;
      this.appendWindowStart = 0; this.appendWindowEnd = Infinity;
      this.onupdatestart = this.onupdate = this.onupdateend = this.onerror = this.onabort = null;
      say({ video: 'start', id: this.id, type });
    }
    // what is buffered: the audio's, which plays alongside (the player waits for both)
    get buffered() {
      const audio = [...realBuffers(this.source)][0];
      return audio ? audio.buffered : empty;
    }
    _done() {
      this.updating = true;
      this._fire('updatestart');
      setTimeout(() => { this.updating = false; this._fire('update'); this._fire('updateend'); }, 0);
    }
    _fire(name) { const e = new Event(name); this['on' + name]?.(e); this.dispatchEvent(e); }
    appendBuffer(data) {
      const bytes = data instanceof ArrayBuffer ? new Uint8Array(data) : new Uint8Array(data.buffer, data.byteOffset, data.byteLength);
      learn(this, bytes);
      let text = '';
      for (let i = 0; i < bytes.length; i += 32768) text += String.fromCharCode.apply(null, bytes.subarray(i, i + 32768));
      say({ video: 'chunk', id: this.id, offset: this.timestampOffset, data: btoa(text) });
      this._done();
    }
    remove(start, end) { say({ video: 'remove', id: this.id, start, end: Number.isFinite(end) ? end : null }); this._done(); }
    abort() { this.updating = false; say({ video: 'abort', id: this.id }); }
    changeType(type) { this.type = type; say({ video: 'type', id: this.id, type }); }
  }
  const empty = { length: 0, start() { throw new DOMException('', 'IndexSizeError'); }, end() { throw new DOMException('', 'IndexSizeError'); } };

  // a MediaSource's video buffer becomes a stand-in when the host plays it; anything else is real
  const standins = new WeakMap();
  const sources = new WeakMap();   // a stand-in's element, once the source is attached to one
  const Make = MediaSource.prototype.addSourceBuffer;
  const realList = Object.getOwnPropertyDescriptor(MediaSource.prototype, 'sourceBuffers').get;
  const realBuffers = source => realList.call(source);
  MediaSource.prototype.addSourceBuffer = function (type) {
    if (!played(type)) return Make.call(this, type);
    const s = new StandIn(this, type);
    (standins.get(this) ?? standins.set(this, []).get(this)).push(s);
    return s;
  };
  for (const name of ['sourceBuffers', 'activeSourceBuffers']) {
    const real = Object.getOwnPropertyDescriptor(MediaSource.prototype, name).get;
    Object.defineProperty(MediaSource.prototype, name, {
      configurable: true,
      get() {
        const list = [...real.call(this), ...(standins.get(this) ?? [])];
        return Object.assign(list, { item: i => list[i] ?? null });
      }
    });
  }
  const Remove = MediaSource.prototype.removeSourceBuffer;
  MediaSource.prototype.removeSourceBuffer = function (b) {
    if (b instanceof StandIn) {
      standins.set(this, (standins.get(this) ?? []).filter(s => s !== b));
      say({ video: 'end', id: b.id });
      return;
    }
    return Remove.call(this, b);
  };

  // the element a played source is attached to: found when its src is the source's object URL
  const Url = URL.createObjectURL;
  const urls = new Map();
  URL.createObjectURL = function (object) {
    const url = Url.call(URL, object);
    if (object instanceof MediaSource) urls.set(url, object);
    return url;
  };
  const elementOf = video => urls.get(video.currentSrc || video.src);
  const playedBy = video => standins.get(elementOf(video)) ?? [];

  // the element looks like it shows the video (an audio-only element has no size and no frames)
  const vw = Object.getOwnPropertyDescriptor(HTMLVideoElement.prototype, 'videoWidth').get;
  const vh = Object.getOwnPropertyDescriptor(HTMLVideoElement.prototype, 'videoHeight').get;
  // its size and frames are the stream's own (read from its boxes); 0 until the stream has said
  const stream = video => playedBy(video).find(s => s.w) ?? playedBy(video)[0];
  Object.defineProperty(HTMLVideoElement.prototype, 'videoWidth', { configurable: true, get() { return vw.call(this) || (stream(this)?.w ?? 0); } });
  Object.defineProperty(HTMLVideoElement.prototype, 'videoHeight', { configurable: true, get() { return vh.call(this) || (stream(this)?.h ?? 0); } });
  const Quality = HTMLVideoElement.prototype.getVideoPlaybackQuality;
  HTMLVideoElement.prototype.getVideoPlaybackQuality = function () {
    const s = stream(this);
    if (!s) return Quality.call(this);
    // the host shows every picture: as many as the clock has passed, none dropped
    return { totalVideoFrames: Math.floor(this.currentTime * rate(s)), droppedVideoFrames: 0, corruptedVideoFrames: 0, creationTime: performance.now() };
  };

  // the clock and the place of every played video: on each change, and four times a second while it plays
  const told = new WeakMap();
  const tell = video => {
    const ids = playedBy(video).map(s => s.id);
    if (!ids.length) return;
    if (!video.dataset.plangVideo) { video.dataset.plangVideo = '1'; (document.head || document.documentElement).appendChild(style); }
    const r = video.getBoundingClientRect();
    const now = { ids, time: video.currentTime, playing: !video.paused && !video.ended && video.readyState > 2, rate: video.playbackRate,
      x: Math.round(r.left), y: Math.round(r.top), w: Math.round(r.width), h: Math.round(r.height),
      shown: document.visibilityState === 'visible' && r.width > 0 && r.height > 0 };
    const last = told.get(video);
    if (last && last.playing === now.playing && last.x === now.x && last.y === now.y && last.w === now.w && last.h === now.h
        && Math.abs(last.time + (now.playing ? 0.25 * now.rate : 0) - now.time) < 0.05 && !now.playing) return;
    told.set(video, now);
    say({ video: 'clock', ...now, key: KEY });
  };
  for (const name of ['play', 'playing', 'pause', 'seeking', 'seeked', 'ratechange', 'waiting', 'ended', 'resize', 'loadedmetadata'])
    document.addEventListener(name, e => e.target instanceof HTMLVideoElement && tell(e.target), true);
  setInterval(() => document.querySelectorAll('video').forEach(tell), 250);
  window.addEventListener('scroll', () => document.querySelectorAll('video').forEach(tell), true);
  window.addEventListener('resize', () => document.querySelectorAll('video').forEach(tell));
})();
