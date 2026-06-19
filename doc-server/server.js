const express = require('express');
const fs = require('fs');
const path = require('path');
const { marked } = require('marked');

const app = express();
app.set('strict routing', true);
const ROOT = path.join(__dirname, '..');
const PORT = process.env.PORT || 8086;

// ── File extension → highlight.js language ──────────────────────────────────
const LANG = { '.cs': 'csharp', '.goal': 'plaintext', '.json': 'json', '.js': 'javascript', '.ts': 'typescript' };
function lang(filePath) { return LANG[path.extname(filePath)] || 'plaintext'; }

// ── PLang code block syntax colouring ───────────────────────────────────────
function highlightPlang(code) {
  return code.split('\n').map(line => {
    // Goal/section name (no leading dash)
    if (!line.startsWith('-') && line.trim() && !line.startsWith(' ')) {
      return `<span style="font-weight:600;color:#1A2128;">${esc(line)}</span>`;
    }
    // Step line
    if (line.startsWith('- ') || line.startsWith('  - ')) {
      const indent = line.match(/^(\s*)/)[1];
      const rest = line.slice(indent.length + 2); // strip "- "
      const colored = rest
        // %variable% → teal
        .replace(/%([^%]+)%/g, (m) => `<span style="color:#2C6E8C;font-weight:500;">${esc(m)}</span>`)
        // file.ext → green (after % replacement so we don't double-process)
        .replace(/\b(\w[\w-]*\.(md|html|pdf|csv|json|txt|goal|cs|js|ts))\b/g,
          (m) => `<span style="color:#4F7C5E;">${esc(m)}</span>`)
        // &lt;-- comment → muted (already escaped)
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
        return `<div style="height:1px;background:#E4E7E4;margin:56px 0 0;"></div><div style="padding:56px 0 0;"><div style="font-family:'IBM Plex Mono',monospace;font-size:12px;letter-spacing:0.16em;text-transform:uppercase;margin-bottom:18px;"><span style="color:#2C6E8C;font-weight:500;">${num}</span><span style="color:#A6AEB4;"> / ${text}</span></div>`;
      }
      if (level === 3) {
        return `<h3 style="font-size:18px;font-weight:500;color:#1A2128;margin:32px 0 10px;letter-spacing:-0.01em;">${text}</h3>`;
      }
      return `<h${level}>${text}</h${level}>`;
    },
    paragraph(text) {
      return `<p style="font-size:clamp(17px,2vw,19px);line-height:1.65;color:#525C64;margin:0 0 20px;max-width:600px;text-wrap:pretty;">${text || ''}</p>`;
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

// ── Build sidebar nav from doc/ folder tree ──────────────────────────────────
function navTree(dir, urlBase) {
  const items = [];
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (!e.isDirectory()) continue;
    const child = path.join(dir, e.name);
    if (!fs.existsSync(path.join(child, 'start.md'))) continue;
    items.push({ label: e.name, href: `${urlBase}/${e.name}/`, children: navTree(child, `${urlBase}/${e.name}`) });
  }
  return items;
}

function navHtml(items, current) {
  if (!items.length) return '';
  return items.map(({ label, href, children }) => {
    const active = current.startsWith(href);
    const color = active ? '#2C6E8C' : '#5C666E';
    return `<a href="${href}" style="display:block;font-family:'IBM Plex Mono',monospace;font-size:13px;color:${color};text-decoration:none;padding:3px 0;">${label}</a>${navHtml(children, current)}`;
  }).join('');
}

// ── HTML shell ───────────────────────────────────────────────────────────────
function page(currentUrl, bodyHtml) {
  sectionCount = 0; // reset for each page render
  const nav = navTree(path.join(ROOT, 'doc'), '/doc');
  const isHome = currentUrl === '/';
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>PLang${isHome ? '' : ' — Docs'}</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@400;500;600&family=Newsreader:ital,wght@0,400;0,500;0,600;1,400&display=swap" rel="stylesheet">
  <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles/github.min.css">
  <style>
    *{box-sizing:border-box;margin:0;padding:0;}
    html,body{background:#F4F5F3;}
    ::selection{background:#CFE0E7;color:#1B2228;}
    body{font-family:'Newsreader',Georgia,serif;color:#222A30;-webkit-font-smoothing:antialiased;text-rendering:optimizeLegibility;}
    pre{background:#f6f8fa;border:1px solid #E4E7E4;border-radius:8px;padding:18px 22px;overflow-x:auto;margin:20px 0;}
    pre code{font-family:'IBM Plex Mono',monospace;font-size:13px;line-height:1.6;background:none;border:none;padding:0;}
    blockquote{border-left:3px solid #E4E7E4;padding-left:16px;color:#7A838A;margin:16px 0;}
  </style>
</head>
<body style="min-height:100vh;background:#F4F5F3;">

  <!-- Flag strip -->
  <div style="height:4px;width:100%;display:flex;">
    <div style="width:13%;background:#02529C;"></div>
    <div style="width:1.5%;background:#FFFFFF;"></div>
    <div style="width:1.6%;background:#DC1E35;"></div>
    <div style="width:1.5%;background:#FFFFFF;"></div>
    <div style="flex:1;background:#02529C;"></div>
  </div>

  <div style="max-width:720px;margin:0 auto;padding:0 28px;">

    <!-- Header -->
    <header style="display:flex;align-items:baseline;justify-content:space-between;padding:30px 0 0;">
      <a href="/" style="text-decoration:none;display:flex;align-items:baseline;gap:2px;">
        <span style="font-size:23px;font-weight:600;letter-spacing:-0.01em;color:#1A2128;">PLang</span>
        <span style="font-family:'IBM Plex Mono',monospace;font-size:12px;color:#97A0A7;">.is</span>
      </a>
      <nav style="display:flex;gap:24px;font-family:'IBM Plex Mono',monospace;font-size:13px;letter-spacing:0.01em;">
        ${navHtml(nav, currentUrl)}
      </nav>
    </header>

    <!-- Hero label -->
    <div style="padding:78px 0 18px;">
      <div style="font-family:'IBM Plex Mono',monospace;font-size:12px;letter-spacing:0.16em;text-transform:uppercase;color:#97A0A7;margin-bottom:26px;">
        <span style="color:#2C6E8C;">plang.is</span>&nbsp;&nbsp;·&nbsp;&nbsp;programming in plain english
      </div>

      <!-- Page content -->
      ${bodyHtml}

    </div>

    <!-- Footer -->
    <div style="height:1px;background:#E4E7E4;margin:64px 0 0;"></div>
    <footer style="padding:36px 0 80px;font-family:'IBM Plex Mono',monospace;font-size:12px;color:#A6AEB4;letter-spacing:0.02em;">
      plang.is
    </footer>

  </div>

  <script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js"></script>
  <script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/languages/csharp.min.js"></script>
  <script>hljs.highlightAll();</script>
</body>
</html>`;
}

// ── Render a markdown file ───────────────────────────────────────────────────
function renderFile(filePath, urlPath) {
  const raw = fs.readFileSync(filePath, 'utf8');
  const expanded = resolveIncludes(raw);
  sectionCount = 0;
  const html = marked.parse(expanded);
  return page(urlPath, html);
}

// ── Routes ───────────────────────────────────────────────────────────────────
function servePage(req, res) {
  const urlPath = req.path.endsWith('/') ? req.path : req.path + '/';
  const rel = urlPath.replace(/^\//, '');
  const candidates = [
    path.join(ROOT, rel, 'start.md'),
    path.join(ROOT, rel.replace(/\/$/, '')),
  ];
  for (const f of candidates) {
    if (fs.existsSync(f)) return res.send(renderFile(f, urlPath));
  }
  res.status(404).send(page(urlPath, '<h1>Not found</h1>'));
}

app.get('/', (req, res) => res.send(renderFile(path.join(ROOT, 'start.md'), '/')));
app.get('/doc', (_, res) => res.redirect('/doc/'));
app.get('/doc/', servePage);
app.get('/doc/*', servePage);

app.listen(PORT, () => console.log(`PLang docs → http://localhost:${PORT}/`));
