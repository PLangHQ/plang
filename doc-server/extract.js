// Build-time structure extractor.
//
// Reads a concept's start.cs and emits start.json describing its shape:
// the class, its constructor parameters (the Data<T> inputs), and its methods
// (name, params, return). The doc markdown references this with [[path/start.json]]
// and the renderer turns it into a component — so parameter tables are never
// hand-maintained and never drift from the code.

const fs = require('fs');
const path = require('path');

// ── Type prettifier: C# surface → the PLang-facing form ─────────────────────
// data.@this<text.@this> → Data<text>;  data.@this → Data;  step.list → step.list
function prettyType(raw) {
  if (!raw) return raw;
  let t = raw.trim();
  t = t.replace(/global::/g, '');
  t = t.replace(/\bdata\.@this\b/g, 'Data');
  t = t.replace(/\bplang\.list\b/g, 'list');
  t = t.replace(/app\.type\./g, '');
  t = t.replace(/\.@this\b/g, '');   // text.@this → text
  return t;
}

// Split a parameter list on top-level commas (ignore commas inside < >).
function splitParams(s) {
  const out = [];
  let depth = 0, cur = '';
  for (const ch of s) {
    if (ch === '<') depth++;
    else if (ch === '>') depth--;
    if (ch === ',' && depth === 0) { out.push(cur); cur = ''; }
    else cur += ch;
  }
  if (cur.trim()) out.push(cur);
  return out.map(p => p.trim()).filter(Boolean);
}

// "data.@this<text.@this> name" → { name, type }
function parseParam(p) {
  // strip default value
  p = p.split('=')[0].trim();
  const i = p.lastIndexOf(' ');
  if (i === -1) return { name: p, type: '' };
  return { name: p.slice(i + 1).trim(), type: prettyType(p.slice(0, i)) };
}

// Pull the leading /// summary that precedes a declaration.
function summaryAbove(lines, declLine) {
  const parts = [];
  for (let i = declLine - 1; i >= 0; i--) {
    const l = lines[i].trim();
    if (l.startsWith('///')) parts.unshift(l.replace(/^\/\/\/\s?/, ''));
    else if (l === '' || l.startsWith('//')) { if (parts.length) break; }
    else break;
  }
  return parts.join(' ').replace(/<\/?summary>/g, '').trim() || null;
}

// Find each top-level class and the {...} body that belongs to it, via brace depth.
function classBlocks(text) {
  const blocks = [];
  const headerRe = /(?:public|internal)?\s*(?:abstract\s+|sealed\s+)?class\s+(\w+)\s*(?:\(([^)]*)\))?[^{]*\{/g;
  let m;
  while ((m = headerRe.exec(text)) !== null) {
    const name = m[1];
    const params = m[2] || '';
    // brace-match from the opening { to find the body
    let depth = 1, i = headerRe.lastIndex;
    for (; i < text.length && depth > 0; i++) {
      if (text[i] === '{') depth++;
      else if (text[i] === '}') depth--;
    }
    blocks.push({ name, params, headerEnd: m.index, body: text.slice(headerRe.lastIndex, i - 1) });
  }
  return blocks;
}

function extract(csSource, relPath) {
  const lines = csSource.split('\n');
  const text = csSource;

  const nsMatch = text.match(/namespace\s+([\w.@]+)\s*;/);
  const namespace = nsMatch ? nsMatch[1].replace(/\.@this/g, '') : null;

  const types = classBlocks(text).map(block => {
    const parameters = block.params ? splitParams(block.params).map(parseParam) : [];

    const methods = [];
    const methodRe = /public\s+(?:async\s+)?([\w.@<>,\s]+?)\s+(\w+)\s*\(([^)]*)\)/g;
    let m;
    while ((m = methodRe.exec(block.body)) !== null) {
      const name = m[2];
      if (name === block.name) continue;          // ctor
      const declLine = text.slice(0, block.headerEnd + m.index).split('\n').length - 1;
      methods.push({
        name,
        returns: prettyType(m[1]),
        params: splitParams(m[3]).map(parseParam),
        summary: summaryAbove(lines, declLine),
      });
    }

    const headerLine = text.slice(0, block.headerEnd).split('\n').length;
    return { name: block.name, parameters, methods, summary: summaryAbove(lines, headerLine - 1) };
  });

  return { namespace, source: relPath, types };
}

// ── CLI: walk app/**/start.cs, write start.json beside each ─────────────────
function walk(dir, root, out) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (e.name.startsWith('.')) continue;
    const full = path.join(dir, e.name);
    if (e.isDirectory()) walk(full, root, out);
    else if (e.name === 'start.cs') {
      const rel = path.relative(root, full);
      const src = fs.readFileSync(full, 'utf8');
      const json = extract(src, rel);
      const dest = full.replace(/\.cs$/, '.json');
      fs.writeFileSync(dest, JSON.stringify(json, null, 2));
      out.push(path.relative(root, dest));
    }
  }
}

module.exports = { extract, prettyType };

if (require.main === module) {
  const ROOT = path.join(__dirname, '..');
  const written = [];
  walk(path.join(ROOT, 'app'), ROOT, written);
  console.log(`generated ${written.length} structure files:`);
  for (const w of written) console.log('  ' + w);
}
