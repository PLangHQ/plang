// Per-line comment client for the doc tree. Anchors to source line numbers the
// server tags on each .cblock; talks to /api/comment{,s}. Light sibling of the
// architect review server.
(function () {
  const DOC_FILE = window.__DOC_FILE__;
  if (!DOC_FILE) return;

  let comments = [];

  const api = (p, opt) => fetch(p, opt).then(r => r.json());
  function esc(s) { const d = document.createElement('div'); d.textContent = s == null ? '' : s; return d.innerHTML; }

  async function load() {
    const j = await api('/api/comments?path=' + encodeURIComponent(DOC_FILE));
    comments = j.comments || [];
    render();
  }

  function render() {
    document.querySelectorAll('.cthread, .ccomposer').forEach(e => e.remove());
    document.querySelectorAll('.cgutter').forEach(g => g.classList.remove('has-comment'));
    for (const blk of document.querySelectorAll('.cblock')) {
      const s = +blk.dataset.start, e = +blk.dataset.end;
      const tops = comments.filter(c => !c.parent_id && c.line >= s && c.line <= e);
      const open = comments.some(c => c.line >= s && c.line <= e && (c.status || 'open') === 'open' && c.author === 'user');
      if (open) blk.querySelector('.cgutter').classList.add('has-comment');
      if (!tops.length) continue;
      const thread = document.createElement('div');
      thread.className = 'cthread';
      for (const c of tops.sort((a, b) => a.line - b.line)) {
        thread.appendChild(commentEl(c));
        comments.filter(r => r.parent_id === c.id).forEach(r => thread.appendChild(commentEl(r)));
      }
      blk.after(thread);
    }
  }

  function btn(cls, label, fn) {
    const b = document.createElement('button');
    b.className = 'cbtn ' + cls; b.textContent = label; b.onclick = fn;
    return b;
  }

  function commentEl(c) {
    const author = c.author || 'user', status = c.status || 'open';
    const div = document.createElement('div');
    div.className = 'ccomment a-' + author + ' s-' + status + (c.parent_id ? ' reply' : '');
    div.innerHTML =
      '<div class="cmeta"><span class="who">' + (author === 'architect' ? '🏛 architect' : '👤 you') + '</span>' +
      '<span class="cline">line ' + c.line + ' · ' + esc(c.ts) + '</span>' +
      (status !== 'open' ? '<span class="cbadge ' + status + '">' + status + '</span>' : '') +
      (c.drifted ? '<span class="cbadge drifted">moved</span>' : '') +
      '</div><div class="ctext">' + esc(c.text) + '</div><div class="cact"></div>';
    const act = div.querySelector('.cact');
    act.appendChild(btn('reply', '↳ reply', () => composer(c.line, div, { parent_id: c.id })));
    if (status === 'open') {
      act.appendChild(btn('resolve', 'resolve', () => patch(c.id, { status: 'resolved' })));
      act.appendChild(btn('disagree', 'disagree', () => patch(c.id, { status: 'disagreed' })));
    } else {
      act.appendChild(btn('reopen', 'reopen', () => patch(c.id, { status: 'open' })));
    }
    act.appendChild(btn('del', '✕', () => del(c.id)));
    return div;
  }

  function composer(line, anchorEl, opts) {
    opts = opts || {};
    document.querySelectorAll('.ccomposer').forEach(e => e.remove());
    const c = document.createElement('div');
    c.className = 'ccomposer';
    c.innerHTML =
      '<textarea placeholder="' + (opts.parent_id ? 'Reply' : 'Comment on line ' + line) + '…"></textarea>' +
      '<div class="crow"><label class="cas"><input type="checkbox"' + (opts.author === 'architect' ? ' checked' : '') + '> as architect</label>' +
      '<span class="cspacer"></span><button class="cbtn save">Save</button><button class="cbtn cancel">Cancel</button></div>';
    anchorEl.insertAdjacentElement('afterend', c);
    const ta = c.querySelector('textarea');
    ta.focus();
    c.querySelector('.cancel').onclick = () => c.remove();
    const save = async () => {
      const text = ta.value.trim();
      if (!text) return;
      const author = c.querySelector('.cas input').checked ? 'architect' : 'user';
      const j = await api('/api/comment', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ file: DOC_FILE, line, text, author, parent_id: opts.parent_id || null }),
      });
      comments = j.comments || comments;
      render();
    };
    c.querySelector('.save').onclick = save;
    ta.addEventListener('keydown', e => { if ((e.metaKey || e.ctrlKey) && e.key === 'Enter') save(); });
  }

  async function patch(id, body) {
    const j = await api('/api/comment?id=' + encodeURIComponent(id), {
      method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
    });
    comments = j.comments || comments;
    render();
  }

  async function del(id) {
    if (!confirm('Delete this comment?')) return;
    const j = await api('/api/comment?id=' + encodeURIComponent(id), { method: 'DELETE' });
    comments = j.comments || comments;
    render();
  }

  document.addEventListener('click', e => {
    const g = e.target.closest('.cgutter');
    if (!g) return;
    e.stopPropagation();
    composer(+g.dataset.line, g.closest('.cblock'), {});
  });

  load();
})();
