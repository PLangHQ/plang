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

// ── Replace [[path/to/file]] with a fenced code block ───────────────────────
function resolveIncludes(md) {
  return md.replace(/\[\[([^\]]+)\]\]/g, (_, rel) => {
    try {
      const src = fs.readFileSync(path.join(ROOT, rel.trim()), 'utf8');
      return `\`\`\`${lang(rel)}\n${src.trimEnd()}\n\`\`\``;
    } catch {
      return `> ⚠️ Could not load \`${rel}\``;
    }
  });
}

// ── Build nav from root-level folders that have a start.md ──────────────────
const SKIP = new Set(['doc-server', 'doc', '.git', '.bot', 'PLang', 'PLang.Tests', 'PLang.Generators', 'PlangConsole', 'Tests', 'os', 'node_modules', 'Documentation', 'characters', 'learnings', 'diary', 'sessions']);

function rootNav(currentUrl) {
  const items = [];
  for (const e of fs.readdirSync(ROOT, { withFileTypes: true })) {
    if (!e.isDirectory() || SKIP.has(e.name) || e.name.startsWith('.')) continue;
    if (!fs.existsSync(path.join(ROOT, e.name, 'start.md'))) continue;
    const href = `/${e.name}/`;
    items.push({ label: e.name, href, active: currentUrl.startsWith(href) });
  }
  return items;
}

// ── Render a page via Liquid template ───────────────────────────────────────
async function page(currentUrl, bodyHtml) {
  const isHome = currentUrl === '/';
  return engine.renderFile('layout', {
    title: isHome ? 'PLang' : 'PLang — Docs',
    content: bodyHtml,
    currentUrl,
    isHome,
    navItems: rootNav(currentUrl),
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

// ── Routes ───────────────────────────────────────────────────────────────────
async function servePage(req, res) {
  const urlPath = req.path.endsWith('/') ? req.path : req.path + '/';
  const rel = urlPath.replace(/^\//, '');
  const candidates = [
    path.join(ROOT, rel, 'start.md'),
    path.join(ROOT, rel.replace(/\/$/, '')),
  ];
  for (const f of candidates) {
    if (fs.existsSync(f)) return res.send(await renderFile(f, urlPath));
  }
  res.status(404).send(await page(urlPath, '<h1>Not found</h1>'));
}

app.get('/', async (req, res) => res.send(await renderFile(path.join(ROOT, 'start.md'), '/')));
app.get('/doc', (_, res) => res.redirect('/doc/'));
app.get('/doc/', servePage);
app.get('/doc/*', servePage);

app.get('/:segment', (req, res) => res.redirect('/' + req.params.segment + '/'));
app.get('/:segment/', servePage);
app.get('/:segment/*', servePage);

app.listen(PORT, () => console.log(`PLang docs → http://localhost:${PORT}/`));
