"""The reference parser's twin (PLang/app/type/item/variable/parser/this.cs) and the .pr form of a row's
variables (variable/serializer/Entry.cs, the hops' Piece): a reference is `%` + a path starting with a
letter, `_` or `!`, closing at the first `%` outside quotes, parentheses and brackets; a space outside
them ends the scan. `parse(text)` answers every reference the text holds as its .pr entry:

    {"text": "%user.address[idx]%", "code": [{"variable": "user"}, {"property": "address"},
     {"index": {"variable": [{"text": "%idx%", "code": [{"variable": "idx"}]}]}}]}
"""

STOP = set('.![]()"\'%,')


def _start(c):
    return c.isalpha() or c in '_!'


class _Reader:
    def __init__(self, text):
        self.text, self.pos, self.end = text, 0, 0

    def close(self, at):
        t = self.text
        if at + 1 >= len(t) or t[at] != '%' or not _start(t[at + 1]): return -1
        depth, quote, i = 0, None, at + 1
        while i < len(t):
            c = t[i]
            if quote:
                if c == '\\': i += 1
                elif c == quote: quote = None
            elif c in '"\'': quote = c
            elif c in '([': depth += 1
            elif c in ')]': depth -= 1
            elif depth == 0 and c == '%': return i
            elif depth == 0 and c.isspace(): return -1
            i += 1
        return -1

    def read(self, at):
        close = self.close(at)
        return None if close < 0 else self.body(at, close)

    def body(self, at, close):
        t = self.text
        text = t[at:close + 1]
        self.pos, self.end = at + 1, close
        bang = self.pos < self.end and t[self.pos] == '!'
        if bang: self.pos += 1
        root = self.name()
        if not root: return None
        code = [{'variable': ('!' + root) if bang else root}]
        while self.pos < self.end:
            c = t[self.pos]
            hop = self.member() if c == '.' else self.binding() if c == '!' else self.index() if c == '[' else None
            if hop is None: return None
            code.append(hop)
        return {'text': text, 'code': code}

    def name(self):
        t, start = self.text, self.pos
        while self.pos < self.end and t[self.pos] not in STOP and not t[self.pos].isspace():
            self.pos += 1
        return t[start:self.pos]

    def member(self):
        self.pos += 1
        if self.pos < self.end and self.text[self.pos] in '"\'':
            key = self.quoted()
            return None if key is None else {'property': key}
        name = self.name()
        if not name: return None
        if self.pos < self.end and self.text[self.pos] == '(':
            values = self.values()
            return None if values is None else {'method': name, 'parameter': values}
        return {'property': name}

    def binding(self):
        self.pos += 1
        name = self.name()
        return {'property': '!' + name} if name else None

    def index(self):
        self.pos += 1
        key = self.value(bare=True)
        if key is None or self.pos >= self.end or self.text[self.pos] != ']': return None
        self.pos += 1
        kind, v = key
        return {'index': {kind: [v] if kind == 'variable' else v}}

    def values(self):
        self.pos += 1
        rows = []
        self.space()
        if self.pos < self.end and self.text[self.pos] == ')':
            self.pos += 1
            return rows
        while self.pos < self.end:
            got = self.value(bare=False)
            if got is None: return None
            kind, v = got
            rows.append({'type': {'name': 'variable'}, 'value': v['text'], 'variable': [v]} if kind == 'variable'
                        else {'type': {'name': kind}, 'value': v})
            self.space()
            if self.pos < self.end and self.text[self.pos] == ',':
                self.pos += 1; self.space(); continue
            if self.pos < self.end and self.text[self.pos] == ')':
                self.pos += 1
                return rows
            return None
        return None

    def value(self, bare):
        """(kind, value): ('text', s), ('number', n), ('bool', b) or ('variable', entry)."""
        if self.pos >= self.end: return None
        t, c = self.text, self.text[self.pos]
        if c in '"\'':
            s = self.quoted()
            return None if s is None else ('text', s)
        if c.isdigit() or (c == '-' and self.pos + 1 < self.end and t[self.pos + 1].isdigit()):
            return self.number()
        if c == '%':
            inner = _Reader(t)
            found = inner.read(self.pos)
            if found is None: return None
            self.pos += len(found['text'])
            return ('variable', found)
        if bare:
            start, depth = self.pos, 0
            while self.pos < self.end and not (depth == 0 and t[self.pos] == ']'):
                if t[self.pos] == '[': depth += 1
                elif t[self.pos] == ']': depth -= 1
                self.pos += 1
            found = _Reader('%' + t[start:self.pos] + '%').read(0)
            return None if found is None else ('variable', found)
        word = self.name()
        return {'true': ('bool', True), 'false': ('bool', False)}.get(word)

    def quoted(self):
        t = self.text
        quote = t[self.pos]; self.pos += 1
        out = []
        while self.pos < self.end:
            c = t[self.pos]; self.pos += 1
            if c == quote: return ''.join(out)
            if c == '\\' and self.pos < self.end:
                c = t[self.pos]; self.pos += 1
            out.append(c)
        return None

    def number(self):
        t, start = self.text, self.pos
        if t[self.pos] == '-': self.pos += 1
        while self.pos < self.end and (t[self.pos].isdigit() or t[self.pos] == '.'): self.pos += 1
        written = t[start:self.pos]
        try: return ('number', int(written))
        except ValueError: pass
        try: return ('number', float(written))
        except ValueError: return None

    def space(self):
        while self.pos < self.end and self.text[self.pos].isspace(): self.pos += 1


def parse(text):
    """Every reference the text holds, in order, each as its .pr entry; one that doesn't parse is left out."""
    found, r, i = [], _Reader(text), 0
    while i < len(text):
        if text[i] == '%':
            close = r.close(i)
            if close >= 0:
                v = r.body(i, close)
                if v: found.append(v)
                i = close
        i += 1
    return found


def read(text, at):
    """The reference whose opening % is at `at`, as its .pr entry, or None (none opens there, or it
    doesn't parse). Its text is exactly what was written, so the reader continues after it."""
    return _Reader(text).read(at)


def placed(text):
    """The text's references in the order written, each with where it ends in the text."""
    out, start = [], 0
    for v in parse(text):
        start = text.index(v['text'], start) + len(v['text'])
        out.append((v, start))
    return out


def is_bare(v):
    """A bare name — %x% — not a setting (%!x%) and not a way into one (%x.y%) (variable.IsBare)."""
    return len(v['code']) == 1 and not v['code'][0]['variable'].startswith('!')


def is_members(v):
    """A value reached by members only — %x%, %user.name% (variable.IsMembers)."""
    return not v['code'][0]['variable'].startswith('!') and all(
        'property' in h and not h['property'].startswith('!') for h in v['code'][1:])


def whole(text):
    """True when the text is one reference and nothing else."""
    found = read(text, 0) if isinstance(text, str) else None
    return found is not None and found['text'] == text


def held(value):
    """The variables a value holds, each once, first written first: those in each of its texts (a
    dict's and a list's in document order)."""
    found = []
    def walk(v):
        if isinstance(v, str): found.extend(parse(v))
        elif isinstance(v, dict):
            for x in v.values(): walk(x)
        elif isinstance(v, list):
            for x in v: walk(x)
    walk(value)
    seen, out = set(), []
    for v in found:
        if v['text'] not in seen:
            seen.add(v['text']); out.append(v)
    return out
