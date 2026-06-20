const express = require('express');
const fs = require('fs');
const path = require('path');
const { marked } = require('marked');
const { Liquid } = require('liquidjs');

const app = express();
app.set('strict routing', true);
const ROOT = path.join(__dirname, '..');
const PORT = process.env.PORT || 8086;

const engine = new Liquid({
  root: path.join(__dirname, 'templates'),
  extname: '.liquid',
  escapeHTML: false,
});

// ── File extension → highlight.js language ──────────────────────────────────
const LANG = { '.cs': 'csharp', '.goal': 'plaintext', '.json': 'json', '.js': 'javascript', '.ts': 'typescript' };
function lang(filePath) { return LANG[path.extname(filePath)] || 'plaintext'; }

// ── PLang code block syntax colouring ───────────────────────────────────────
function highlightPlang(code) {
  return code.split('\n').map(line => {
    if (!line.startsWith('-') && line.trim() && !line.startsWith(' ')) {
      return `<span style="font-weight:600;color:#1A2128;">${esc(line)}</span>`;
    }
    if (line.startsWith('- ') || line.startsWith('  - ')) {
      const indent = line.match(/^(\s*)/)[1];
      const rest = line.slice(indent.length + 2);
      const colored = rest
        .replace(/%([^%]+)%/g, (m) => `<span style="color:#2C6E8C;font-weight:500;">${esc(m)}</span>`)
        .replace(/\b(\w[\w-]*\.(md|html|pdf|csv|json|txt|goal|cs|js|ts))\b/g,
          (m) => `<span style="color:#4F7C5E;">${esc(m)}</span>`)
        .replace(/(&lt;--[^<]*)$/, m => `<span style="color:#97A0A7;font-style:italic;">${m}</span>`);
      return `${esc(indent)}<span style="color:#AEB6BC;">- </span>${colored}`;
    }
    return esc(line);
  }).join('\n');
}

function esc(s) {
  if (!s) return '';
  return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

// ── Custom marked renderer (v12 uses legacy positional-arg API) ──────────────
let sectionCount = 0;

marked.use({
  renderer: {
    code(code, lang) {
      code = code || ''; lang = lang || '';
      if (lang === 'plang') {
        return `<div style="font-family:'IBM Plex Mono',monospace;font-size:15px;line-height:2.1;background:#FFFFFF;border:1px solid #E4E7E4;border-radius:12px;padding:26px 28px;color:#3A434C;box-shadow:0 1px 2px rgba(20,30,40,0.04),0 14px 30px -22px rgba(20,30,40,0.22);margin:24px 0;white-space:pre;">${highlightPlang(code)}</div>`;
      }
      const cls = lang ? ` class="language-${lang}"` : '';
      return `<pre><code${cls}>${esc(code)}</code></pre>`;
    },
    heading(text, level) {
      text = text || ''; level = level || 1;
      if (level === 1) {
        sectionCount = 0;
        return `<h1 style="font-size:clamp(36px,5.6vw,56px);line-height:1.08;font-weight:500;letter-spacing:-0.02em;margin:0 0 28px;color:#161D23;text-wrap:balance;">${text}</h1>`;
      }
      if (level === 2) {
        sectionCount++;
        const num = String(sectionCount).padStart(2, '0');
        return `<div style="height:1px;background:#E4E7E4;margin:56px 0 0;"></div><div style="padding:56px 0 0;"><div style="font-family:'IBM Plex Mono',monospace;font-size:12px;letter-spacing:0.16em;text-transform:uppercase;margin-bottom:18px;"><span style="color:#2C6E8C;font-weight:500;">${num}</span><span style="color:#A6AEB4;">&nbsp;/</span></div><h2 style="font-size:clamp(26px,3.4vw,33px);line-height:1.22;font-weight:500;letter-spacing:-0.01em;margin:0 0 28px;color:#1A2128;max-width:600px;text-wrap:balance;">${text}</h2>`;
      }
      if (level === 3) {
        return `<h3 style="font-size:19px;font-weight:500;color:#1A2128;margin:32px 0 10px;letter-spacing:-0.01em;">${text}</h3>`;
      }
      return `<h${level}>${text}</h${level}>`;
    },
    paragraph(text) {
      return `<p style="font-size:19px;line-height:1.65;color:#525C64;margin:0 0 20px;max-width:600px;text-wrap:pretty;">${text || ''}</p>`;
    },
    hr() {
      return `<div style="height:1px;background:#E4E7E4;margin:56px 0 0;"></div>`;
    },
    list(body, ordered) {
      const tag = ordered ? 'ol' : 'ul';
      return `<${tag} style="font-size:18px;line-height:1.65;padding-left:22px;margin:0 0 20px;">${body}</${tag}>`;
    },
    listitem(text) {
      return `<li style="margin:6px 0;color:#525C64;">${text}</li>`;
    },
    link(href, title, text) {
      return `<a href="${href || '#'}" style="color:#2C6E8C;">${text || ''}</a>`;
    },
    codespan(code) {
      return `<code style="font-family:'IBM Plex Mono',monospace;font-size:0.84em;background:#E9F0F3;color:#2C6E8C;padding:3px 7px;border-radius:5px;border:1px solid #D9E6EB;">${esc(code)}</code>`;
    },
  }
});

// ── Replace [[path/to/file]] with an include ────────────────────────────────
// A .json include renders the generated structure (params/methods) as a
// component; anything else embeds as a fenced code block.
function resolveIncludes(md) {
  return md.replace(/\[\[([^\]]+)\]\]/g, (_, rel) => {
    const clean = rel.trim();
    try {
      if (clean.endsWith('.json')) {
        const data = JSON.parse(fs.readFileSync(path.join(ROOT, clean), 'utf8'));
        return renderStructure(data);
      }
      const src = fs.readFileSync(path.join(ROOT, clean), 'utf8');
      return `\`\`\`${lang(clean)}\n${src.trimEnd()}\n\`\`\``;
    } catch {
      return `> ⚠️ Could not load \`${clean}\``;
    }
  });
}

// ── Render a generated structure JSON as HTML ───────────────────────────────
// One card per type, read top-to-bottom as a signature:
//   the constructor (the Data<T> it's built from), then the methods it answers.
const MONO = "'IBM Plex Mono',monospace";
const C = { name: '#161D23', type: '#2C6E8C', mute: '#A6AEB4', arrow: '#7C868D' };
const nm  = t => `<span style="color:${C.name};">${esc(t)}</span>`;
const ty  = t => `<span style="color:${C.type};">${esc(t)}</span>`;
const mut = t => `<span style="color:${C.mute};">${esc(t)}</span>`;

function renderStructure(data) {
  const types = data.types || [data];
  let html = `<div style="margin:28px 0;display:flex;flex-direction:column;gap:14px;">`;

  for (const t of types) {
    html += `<div style="border:1px solid #E4E7E4;border-radius:12px;background:#FFFFFF;overflow:hidden;">`;

    // header: type name + namespace
    html += `<div style="display:flex;align-items:baseline;gap:10px;padding:13px 20px;border-bottom:1px solid #EEF0EE;background:#FCFCFB;">`;
    html += `<span style="font-family:${MONO};font-size:15px;font-weight:600;color:${C.name};">${esc(t.name)}</span>`;
    if (data.namespace) html += `<span style="font-family:${MONO};font-size:12px;color:${C.mute};">${esc(data.namespace)}</span>`;
    html += `</div>`;

    if (t.summary)
      html += `<p style="font-size:14px;line-height:1.55;color:#6B757D;margin:0;padding:13px 20px 0;">${esc(t.summary)}</p>`;

    html += `<div style="font-family:${MONO};font-size:14px;line-height:1.85;padding:15px 20px;">`;

    // constructor — what the type is built from
    const params = t.parameters || [];
    if (params.length) {
      html += `<div>${nm(t.name)}${mut('(')}</div>`;
      params.forEach((p, i) => {
        const comma = i < params.length - 1 ? mut(',') : '';
        html += `<div style="display:flex;padding-left:22px;gap:18px;">`
              + `<span style="min-width:88px;color:${C.name};">${esc(p.name)}</span>`
              + `<span>${ty(p.type)}${comma}</span></div>`;
      });
      html += `<div>${mut(')')}</div>`;
    } else {
      html += `<div>${nm(t.name)}${mut('()')}</div>`;
    }

    // methods — what the type answers. name(args) → return
    const methods = (t.methods || []).filter(m => m.name !== 'list');
    if (methods.length) {
      html += `<div style="height:1px;background:#F0F1EF;margin:13px 0;"></div>`;
      for (const m of methods) {
        const args = (m.params || []).map(p => `${ty(p.type)} ${nm(p.name)}`).join(mut(', '));
        html += `<div>${nm(m.name)}${mut('(')}${args}${mut(')')} ${mut('→')} ${ty(m.returns)}</div>`;
        if (m.summary)
          html += `<div style="font-size:13px;color:${C.mute};padding-left:22px;margin:-1px 0 7px;line-height:1.5;">${esc(m.summary)}</div>`;
      }
    }

    html += `</div></div>`;
  }

  html += `</div>`;
  return html;
}

// ── Build nav from root-level folders that have a start.md ──────────────────
const SKIP = new Set(['doc-server', '.git', '.bot', 'PLang', 'PLang.Tests', 'PLang.Generators', 'PlangConsole', 'Tests', 'os', 'node_modules', 'Documentation', 'characters', 'learnings', 'diary', 'sessions']);

function hasMd(dir) {
  try {
    return fs.readdirSync(dir).some(f => f.endsWith('.md'));
  } catch { return false; }
}

function rootNav(currentUrl) {
  const items = [];
  for (const e of fs.readdirSync(ROOT, { withFileTypes: true })) {
    if (!e.isDirectory() || SKIP.has(e.name) || e.name.startsWith('.')) continue;
    const dirPath = path.join(ROOT, e.name);
    if (!fs.existsSync(path.join(dirPath, 'start.md')) && !hasMd(dirPath)) continue;
    const href = `/${e.name}/`;
    items.push({ label: e.name, href, active: currentUrl.startsWith(href) });
  }
  return items;
}

// ── Doc tree (left sidebar) ──────────────────────────────────────────────────
function buildDocTree(dir, urlBase) {
  const nodes = [];
  const isRoot = dir === ROOT;
  let entries;
  try { entries = fs.readdirSync(dir, { withFileTypes: true }); } catch { return nodes; }
  for (const e of entries.sort((a, b) => a.name.localeCompare(b.name))) {
    if (e.name.startsWith('.')) continue;
    if (isRoot && SKIP.has(e.name)) continue;
    if (e.isDirectory()) {
      const href = urlBase + e.name + '/';
      const hasStart = fs.existsSync(path.join(dir, e.name, 'start.md'));
      const children = buildDocTree(path.join(dir, e.name), href);
      // Skip folders that have no page and no documented children (e.g. a
      // concept with only start.cs so far) — don't litter the nav with dead labels.
      if (!hasStart && children.length === 0) continue;
      nodes.push({ label: e.name, href: hasStart ? href : null, children });
    } else if (e.name.endsWith('.md') && e.name !== 'start.md') {
      const slug = e.name.replace(/\.md$/, '');
      nodes.push({ label: slug.replace(/-/g, ' '), href: urlBase + slug + '/', children: [] });
    }
  }
  return nodes;
}

function renderDocNavHtml(nodes, currentUrl, depth) {
  if (!nodes.length) return '';
  depth = depth || 0;
  const indent = depth * 14;
  let html = `<ul style="list-style:none;padding:0;margin:0;">`;
  for (const n of nodes) {
    const active = currentUrl === n.href || (n.href && currentUrl.startsWith(n.href));
    const color = active ? '#161D23' : '#6B757D';
    const weight = active ? '500' : '400';
    const pad = `4px 0 4px ${8 + indent}px`;

    // A folder with children → foldable <details>. Open when the active page is
    // inside it (or when there's no link target, so the label stays reachable).
    if (n.children && n.children.length) {
      const within = n.href && currentUrl.startsWith(n.href);
      const open = within || !n.href ? ' open' : '';
      const labelInner = n.href
        ? `<a href="${n.href}" style="font-family:'IBM Plex Mono',monospace;font-size:13px;color:${color};font-weight:${weight};text-decoration:none;">${n.label}</a>`
        : `<span style="font-family:'IBM Plex Mono',monospace;font-size:13px;color:#3A434C;font-weight:500;">${n.label}</span>`;
      html += `<li><details${open}>`;
      html += `<summary style="list-style:none;cursor:pointer;padding:${pad};display:flex;align-items:center;gap:6px;">`;
      html += `<span class="arrow" style="font-size:9px;color:#A6AEB4;transition:transform .12s;display:inline-block;">▶</span>${labelInner}</summary>`;
      html += renderDocNavHtml(n.children, currentUrl, depth + 1);
      html += `</details></li>`;
      continue;
    }

    // Leaf
    const label = n.href
      ? `<a href="${n.href}" style="display:block;font-family:'IBM Plex Mono',monospace;font-size:13px;color:${color};font-weight:${weight};text-decoration:none;padding:${pad};border-radius:4px;">${n.label}</a>`
      : `<span style="display:block;font-family:'IBM Plex Mono',monospace;font-size:13px;color:#B0B8BF;padding:${pad};">${n.label}</span>`;
    html += `<li>${label}</li>`;
  }
  html += '</ul>';
  return html;
}

function docNav(currentUrl) {
  // The nav roots: the canonical app/ tree, and the modules folder. Each is a
  // foldable group — walk only these, not the whole repo.
  const tree = [
    { label: 'app', href: '/app/', children: buildDocTree(path.join(ROOT, 'app'), '/app/') },
    { label: 'modules', href: '/docs/modules/', children: buildDocTree(path.join(ROOT, 'docs', 'modules'), '/docs/modules/') },
  ];
  return renderDocNavHtml(tree, currentUrl, 0);
}

// ── Render a page via Liquid template ───────────────────────────────────────
async function page(currentUrl, bodyHtml, opts = {}) {
  const isHome = currentUrl === '/';
  const sidebar = docNav(currentUrl);
  const content = `<div style="display:grid;grid-template-columns:220px 1fr;gap:64px;align-items:start;">` +
    `<aside style="position:sticky;top:40px;">` +
    `<div style="border-left:1px solid #E4E7E4;">${sidebar}</div>` +
    `</aside>` +
    `<main style="min-width:0;">${bodyHtml}</main>` +
    `</div>`;
  return engine.renderFile('layout', {
    title: opts.title || (isHome ? 'plang' : 'plang — Docs'),
    content,
    currentUrl,
    isHome,
    query: opts.query || '',
  });
}

// ── Render a markdown file ───────────────────────────────────────────────────
async function renderFile(filePath, urlPath) {
  const raw = fs.readFileSync(filePath, 'utf8');
  const expanded = resolveIncludes(raw);
  sectionCount = 0;
  const html = marked.parse(expanded);
  return page(urlPath, html);
}

// ── Auto directory listing ────────────────────────────────────────────────────
function walkMd(dir, base) {
  const entries = { files: [], dirs: {} };
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (e.name.startsWith('.')) continue;
    if (e.isDirectory()) {
      entries.dirs[e.name] = walkMd(path.join(dir, e.name), base);
    } else if (e.name.endsWith('.md') && e.name !== 'start.md' && e.name !== 'index.md') {
      entries.files.push(e.name);
    }
  }
  return entries;
}

function renderTree(entries, urlBase) {
  let html = '<ul style="list-style:none;padding:0;margin:0;">';
  for (const f of entries.files.sort()) {
    const label = f.replace(/\.md$/, '').replace(/-/g, ' ');
    const href = urlBase + f.replace(/\.md$/, '') + '/';
    html += `<li style="margin:0;"><a href="${href}" style="display:block;font-family:'IBM Plex Mono',monospace;font-size:15px;color:#2C6E8C;text-decoration:none;padding:8px 0;border-bottom:1px solid #F0F1EF;">${label}</a></li>`;
  }
  for (const [dir, sub] of Object.entries(entries.dirs).sort()) {
    html += `<li style="margin:20px 0 6px;"><span style="font-family:'IBM Plex Mono',monospace;font-size:12px;letter-spacing:0.14em;text-transform:uppercase;color:#A6AEB4;">${dir}</span>`;
    html += renderTree(sub, urlBase + dir + '/');
    html += '</li>';
  }
  html += '</ul>';
  return html;
}

async function dirListing(dirPath, urlPath, title) {
  const entries = walkMd(dirPath, urlPath);
  const tree = renderTree(entries, urlPath);
  const body = `<h1 style="font-size:clamp(36px,5.6vw,56px);line-height:1.08;font-weight:500;letter-spacing:-0.02em;margin:0 0 40px;color:#161D23;">${title}</h1>${tree}`;
  return page(urlPath, body);
}

// ── Routes ───────────────────────────────────────────────────────────────────

async function servePage(req, res) {
  const urlPath = req.path.endsWith('/') ? req.path : req.path + '/';
  const rel = urlPath.replace(/^\//, '');
  const bare = rel.replace(/\/$/, '');
  const candidates = [
    path.join(ROOT, rel, 'start.md'),
    path.join(ROOT, bare + '.md'),
    path.join(ROOT, bare),
  ];
  for (const f of candidates) {
    if (!fs.existsSync(f)) continue;
    const stat = fs.statSync(f);
    if (stat.isFile()) return res.send(await renderFile(f, urlPath));
    if (stat.isDirectory()) {
      const title = bare.split('/').pop() || bare;
      return res.send(await dirListing(f, urlPath, title));
    }
  }
  res.status(404).send(await page(urlPath, '<h1>Not found</h1>'));
}

app.get('/ask', async (req, res) => {
  const q = (req.query.q || '').trim();
  let body;
  if (!q) {
    body = `<p style="font-size:19px;line-height:1.65;color:#525C64;">Type a question in the bar above.</p>`;
  } else {
    body = `<h1 style="font-size:clamp(28px,4vw,42px);line-height:1.12;font-weight:500;letter-spacing:-0.02em;margin:0 0 28px;color:#161D23;">${esc(q)}</h1>` +
      `<p style="font-size:19px;line-height:1.65;color:#525C64;margin:0 0 20px;max-width:600px;">` +
      `Language answers are coming. For now, browse the docs using the sidebar — start with ` +
      `<a href="/" style="color:#2C6E8C;">start</a> or jump straight to ` +
      `<a href="/app/" style="color:#2C6E8C;">app/</a>.` +
      `</p>`;
  }
  res.send(await page('/ask', body, { title: q ? `${q} — plang` : 'Ask plang', query: q }));
});

app.get('*', servePage);

app.listen(PORT, () => console.log(`plang docs → http://localhost:${PORT}/`));
