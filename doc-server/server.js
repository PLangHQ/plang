const express = require('express');
const fs = require('fs');
const path = require('path');
const { marked } = require('marked');

const app = express();
const ROOT = path.join(__dirname, '..');
const PORT = process.env.PORT || 8086;

// ── File extension → highlight.js language ──────────────────────────────────
const LANG = { '.cs': 'csharp', '.goal': 'plaintext', '.json': 'json', '.js': 'javascript', '.ts': 'typescript', '.md': 'markdown' };
function lang(filePath) { return LANG[path.extname(filePath)] || 'plaintext'; }

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
  let items = [];
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
  const lis = items.map(({ label, href, children }) => {
    const active = current.startsWith(href) ? ' class="active"' : '';
    return `<li><a href="${href}"${active}>${label}</a>${navHtml(children, current)}</li>`;
  }).join('');
  return `<ul>${lis}</ul>`;
}

// ── HTML shell ───────────────────────────────────────────────────────────────
function page(title, currentUrl, bodyHtml) {
  const nav = navHtml(navTree(path.join(ROOT, 'doc'), '/doc'), currentUrl);
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>${title} — PLang</title>
  <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles/github.min.css">
  <style>
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
    body { font: 16px/1.6 -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; color: #1f2328; background: #fff; display: flex; min-height: 100vh; }

    /* sidebar */
    nav { width: 230px; flex-shrink: 0; background: #f6f8fa; border-right: 1px solid #d0d7de; padding: 28px 16px; }
    nav .logo { font-size: 15px; font-weight: 700; margin-bottom: 20px; }
    nav .logo a { color: #1f2328; text-decoration: none; }
    nav ul { list-style: none; }
    nav ul ul { margin-left: 14px; border-left: 1px solid #d0d7de; padding-left: 10px; }
    nav li { margin: 2px 0; }
    nav a { display: block; padding: 4px 8px; border-radius: 5px; font-size: 14px; color: #1f2328; text-decoration: none; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    nav a:hover { background: #eaeef2; }
    nav a.active { background: #ddeeff; font-weight: 600; color: #0969da; }

    /* content */
    main { flex: 1; padding: 48px 64px; max-width: 900px; min-width: 0; }
    h1 { font-size: 26px; font-weight: 700; margin-bottom: 20px; padding-bottom: 12px; border-bottom: 1px solid #d0d7de; }
    h2 { font-size: 20px; font-weight: 600; margin: 36px 0 10px; padding-bottom: 6px; border-bottom: 1px solid #eaeef2; }
    h3 { font-size: 16px; font-weight: 600; margin: 24px 0 8px; }
    p  { margin-bottom: 14px; color: #444c56; }
    a  { color: #0969da; }

    /* code */
    pre  { background: #f6f8fa; border: 1px solid #d0d7de; border-radius: 6px; padding: 18px; overflow-x: auto; margin: 18px 0; }
    code { font: 13px/1.5 "SFMono-Regular", Consolas, "Liberation Mono", monospace; }
    p code, li code { background: #f6f8fa; border: 1px solid #d0d7de; border-radius: 4px; padding: 2px 5px; font-size: 13px; }
    pre code { background: none; border: none; padding: 0; }

    /* misc */
    table { border-collapse: collapse; width: 100%; margin: 16px 0; font-size: 14px; }
    th, td { border: 1px solid #d0d7de; padding: 8px 14px; text-align: left; }
    th { background: #f6f8fa; font-weight: 600; }
    hr { border: none; border-top: 1px solid #d0d7de; margin: 32px 0; }
    blockquote { border-left: 4px solid #d0d7de; padding-left: 16px; color: #57606a; margin: 16px 0; }
    ul, ol { padding-left: 24px; margin-bottom: 14px; }
    li { margin: 4px 0; color: #444c56; }
  </style>
</head>
<body>
  <nav>
    <div class="logo"><a href="/doc/">PLang Docs</a></div>
    <ul><li><a href="/doc/"${currentUrl === '/doc/' ? ' class="active"' : ''}>start</a>${nav}</li></ul>
  </nav>
  <main>
    ${bodyHtml}
  </main>
  <script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js"></script>
  <script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/languages/csharp.min.js"></script>
  <script>hljs.highlightAll();</script>
</body>
</html>`;
}

// ── Render a markdown file to an HTML page ───────────────────────────────────
function renderFile(filePath, urlPath) {
  const raw = fs.readFileSync(filePath, 'utf8');
  const expanded = resolveIncludes(raw);
  const html = marked.parse(expanded);
  const title = (raw.match(/^#\s+(.+)$/m) || [])[1] || path.basename(filePath);
  return page(title, urlPath, html);
}

// ── Routes ───────────────────────────────────────────────────────────────────
app.get('/', (_, res) => res.redirect('/doc/'));

app.get('/doc', (_, res) => res.redirect('/doc/'));

app.get('/doc/*', (req, res) => {
  let urlPath = req.path.endsWith('/') ? req.path : req.path + '/';
  const rel = req.path.replace(/^\//, '');           // e.g. "doc/app/goal/"
  const candidates = [
    path.join(ROOT, rel, 'start.md'),                // /doc/app/goal/ → doc/app/goal/start.md
    path.join(ROOT, rel.replace(/\/$/, '')),          // /doc/app/goal/start.md (direct)
  ];

  for (const f of candidates) {
    if (fs.existsSync(f)) {
      return res.send(renderFile(f, urlPath));
    }
  }

  res.status(404).send(page('Not found', urlPath, '<h1>Not found</h1>'));
});

app.listen(PORT, () => console.log(`PLang docs → http://localhost:${PORT}/doc/`));
