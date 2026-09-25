"""Formal — a step's actions written as calls, and the two directions between it and the .pr rows.

    [0] file.read(Path: path = "orders/%orderId%.json"); variable.set(Name: variable = %order%, Value: item = %!data%)
    [3] error.handle(RetryCount: number = 2, Order: choice<errororder> = "GoalFirst", Recovery: list<action> = [goal.call(Name: text = "FixProperties")]) {
            goal.call(Name: text = "Compile")
        }

Grammar (ours, strict — the programmer's language has no syntax, this notation does). Whitespace,
new lines included, may sit between any two tokens.

    step      = ["[" index "]"] action { ";" action }
    action    = module "." name "(" [ prop { "," prop } ] ")" [ "{" actions "}" ]
    actions   = action { ";" action }
    prop      = Name [ ":" type ] ( "=" | "?=" ) value         ?= : a frozen default
    type      = name [ "<" kind ">" ]
    value     = "text" | number | true | false | null | %variable% | list | dict | action
    list      = "[" [ value { "," value } ] "]"
    dict      = "{" [ key ":" value { "," key ":" value } ] "}"      key = name | "text"

`{ }` holds the actions an action contains: a condition's body (its child), or the one action a
modifier wraps. A modifier's recovery is its `Recovery` property. Nesting gives the wrap order —
`error.handle(…) { cache.wrap(…) { file.read(…) } }` — and the rows keep it outermost first in the
wrapped action's `modifier` list.

A value's TYPE never comes from the model. Written or not, it is the property's declared type, or —
where that is `item`, the open slot — the written type if any, else the literal's own (a quoted text is
text, 5 a number, a %variable% item). A type written against a declared one must equal it. A dict
given to a `list` property is its rows (goal.call's Parameter={to: %x%} → one row per key); a row is
written like a property, `{to: item = %x%}`, its type optional on input and then the literal's. A dict
LITERAL value keeps untyped entries (`Value={"name": "a"}`): the dict is the type.

    parse(text) -> [action rows]         one step
    parse_answer(text) -> {index: rows}  a whole answer, one `[i]` per step
    write(rows) -> text                  the .pr rows back to formal, types always written
    is_formal(text) -> bool              a .goal step written in formal (parsed directly)
"""
import json, re
import build_pr as b

CONDITIONS = {('condition', 'if'), ('condition', 'elseif'), ('condition', 'else')}
RECOVERY = 'Recovery'   # a modifier's property holding the actions it runs when the wrapped action fails
VARIABLE = re.compile(r'%[^%\s]+%')

class FormalError(Exception):
    """Where the formal stops making sense: line and column (1-based) and what was expected."""
    def __init__(self, message, text, pos):
        self.line = text.count('\n', 0, pos) + 1
        self.col = pos - (text.rfind('\n', 0, pos) + 1) + 1
        self.reason = message
        super().__init__(f'line {self.line}, column {self.col}: {message}')

# ---------------------------------------------------------------- the catalogue
def properties(module, name):
    """{Name: {type, options, …}} the action declares — the catalogue's rows, action- and goal-typed included."""
    return dict(b.declared(module, name)[0])

def is_modifier(module, name):
    return b.declared(module, name)[1]

def takes_recovery(module, name):
    return (module, name) == ('error', 'handle')

def known(module, name):
    return b.handler_source(module, name) is not None

def face(t):
    """A row's type as formal writes it: `name` or `name<kind>`."""
    return f'{t["name"]}<{t["kind"]}>' if t.get('kind') else t['name']

def typed(declared, value, written=None):
    """The row's type: the declared one; in an open (`item`) slot the written one, else the literal's."""
    if declared and declared != 'item': return _split(declared)
    if written: return _split(written)
    if isinstance(value, bool): return {'name': 'bool'}
    if isinstance(value, (int, float)): return {'name': 'number'}
    if isinstance(value, list): return {'name': 'list'}
    if isinstance(value, dict): return {'name': 'action'} if value.get('module') else {'name': 'dict'}
    if value is None: return {'name': 'item'}
    return {'name': 'item'} if VARIABLE.fullmatch(value) else {'name': 'text'}

class Args(dict):
    """A `{ }` value as read: its entries, and the types written on any of them (`kind: text = "x"`)."""
    def __init__(self):
        super().__init__()
        self.types = {}

def _split(t):
    m = re.fullmatch(r'(\w+)<(\w+)>', t)
    return {'name': m.group(1), 'kind': m.group(2)} if m else {'name': t}

# ---------------------------------------------------------------- reading
class _Reader:
    def __init__(self, text):
        self.text, self.pos = text, 0

    def fail(self, message, at=None):
        if at is not None: self.pos = at
        raise FormalError(message, self.text, self.pos)

    def space(self):
        while self.pos < len(self.text) and self.text[self.pos] in ' \t\r\n':
            self.pos += 1

    def peek(self, s):
        self.space()
        return self.text.startswith(s, self.pos)

    def take(self, s):
        self.space()
        if not self.text.startswith(s, self.pos): self.fail(f'expected `{s}`')
        self.pos += len(s)

    def ident(self):
        self.space()
        m = re.compile(r'[A-Za-z_][A-Za-z0-9_]*').match(self.text, self.pos)
        if not m: self.fail('expected a name')
        self.pos = m.end()
        return m.group()

    def at_action(self):
        self.space()
        return re.compile(r'[A-Za-z_]\w*\.[A-Za-z_]\w*\s*\(').match(self.text, self.pos) is not None

    def at_end(self):
        self.space()
        return self.pos >= len(self.text)

    def actions(self, closing=None):
        """action { ";" action } — up to `closing` or the end."""
        out = [self.action()]
        while not (self.peek(closing) if closing else self.at_end()):
            m = re.compile(r'([A-Za-z_]\w*)\.([A-Za-z_]\w*)\s*\(').match(self.text, self.pos)
            if m and known(*m.groups()) and is_modifier(*m.groups()):
                self.fail(f'`{m.group(1)}.{m.group(2)}` wraps the action it modifies: {m.group(1)}.{m.group(2)}(…) {{ action }}')
            if not self.peek(';'):
                if self.peek('·'): self.fail('separate a step\'s actions with `;`, not `·`')
                if self.at_action(): self.fail('separate a step\'s actions with `;`')
                self.fail(f'expected `;`{" or `" + closing + "`" if closing else ""}' + ('' if closing else ' or the end of the step'))
            self.take(';')
            out.append(self.action())
        return out

    # action = module "." name "(" props ")" [ "{" actions "}" ]
    def action(self):
        self.space()
        start = self.pos
        module = self.ident(); self.take('.'); name = self.ident()
        if not known(module, name): self.fail(f'`{module}.{name}` is not an action', start)
        declared = properties(module, name)
        modifier = is_modifier(module, name)
        self.take('(')
        rows, recovery = [], None
        while not self.peek(')'):
            if self.at_end(): self.fail(f'`{module}.{name}(` is not closed: expected `)`')
            if rows or recovery is not None: self.take(',')
            self.space()
            at = self.pos
            prop = self.ident()
            if prop == RECOVERY and takes_recovery(module, name):
                if recovery is not None: self.fail(f'`{prop}` is given twice', at)
                self.written_type(prop, 'list<action>')
                self.take('=')
                recovery = self.value()
                if not (isinstance(recovery, list) and recovery and all(isinstance(x, dict) and x.get('module') for x in recovery)):
                    self.fail(f'`{prop}` is a list of actions: [goal.call(Name="X")]', at)
                continue
            if prop not in declared:
                have = list(declared) + ([RECOVERY] if takes_recovery(module, name) else [])
                self.fail(f'`{module}.{name}` has no property `{prop}` (it has {", ".join(have) or "none"})', at)
            if any(r['name'] == prop for r in rows): self.fail(f'`{prop}` is given twice', at)
            rows.append(self.row(prop, declared[prop]))
        self.take(')')
        act = {'module': module, 'name': name, 'property': rows, 'modifier': []}
        if recovery is not None: act['recovery'] = recovery
        brace = self.pos
        if self.peek('{'):
            self.space(); brace = self.pos
            if not (modifier or (module, name) in CONDITIONS):
                self.fail(f'`{module}.{name}` contains no actions: only a condition (its body) and a modifier (the action it wraps) take {{ }}; '
                          f'write the actions one after the other: {module}.{name}(…); next.action(…)')
            self.take('{')
            body = self.actions('}')
            self.take('}')
            if modifier:
                if len(body) != 1:
                    self.fail(f'`{module}.{name}` wraps one action; to wrap it in more modifiers, nest them', brace)
                wrapped = body[0]
                wrapped['modifier'] = [act] + wrapped.get('modifier', [])
                return wrapped
            # formal has no place for the body's words: the child's text is the body in formal
            act['child'] = [{'text': write_actions(body, types=False), 'action': body}]
        elif modifier:
            self.fail(f'`{module}.{name}` is a modifier: it wraps an action — {module}.{name}(…) {{ action }}', start)
        return act

    def written_type(self, prop, declared):
        """An optional `: type` after the name. Against a declared type it must be that type."""
        if not self.peek(':'): return None
        self.take(':')
        self.space()
        at = self.pos
        m = re.compile(r'\w+(<\w+>)?').match(self.text, self.pos)
        if not m: self.fail('expected a type after `:`')
        self.pos = m.end()
        if declared and declared != 'item' and m.group() != declared:
            self.fail(f'`{prop}` is {declared}, not {m.group()}', at)
        return m.group()

    def row(self, prop, spec):
        declared = spec['type']
        written = self.written_type(prop, declared)
        self.space()
        frozen = self.text.startswith('?=', self.pos)
        if self.text.startswith('==', self.pos):
            self.fail(f'one `=` gives a value: {prop}="=="')
        self.take('?=' if frozen else '=')
        self.space()
        at = self.pos
        # A choice's option may stand bare (Operator=isempty): it is a name out of a closed set, not
        # a text, so it can't be misread. The writer always quotes it.
        bare = re.compile(r'[A-Za-z_]\w*(?![\w.(])').match(self.text, self.pos) if spec.get('options') else None
        if bare and bare.group() in spec['options'] and bare.group() not in ('true', 'false', 'null'):
            self.pos = bare.end()
            value = bare.group()
        elif bare and bare.group() not in ('true', 'false', 'null'):
            self.fail(f'`{prop}` is one of {", ".join(spec["options"])}; `{bare.group()}` is not', at)
        else:
            value = self.value()
        if spec.get('options') and value not in spec['options']:
            self.fail(f'`{prop}` is one of {", ".join(spec["options"])}; `{value}` is not', at)
        if declared == 'variable' and not (isinstance(value, str) and VARIABLE.fullmatch(value)):
            self.fail(f'`{prop}` names a variable: write it with its % signs', at)
        # A dict handed to a list property is its rows: Parameter={to: %x%} → [{name: to, …}], each
        # typed like any row — the written type, else the literal's (the slot is open).
        if declared.startswith('list') and isinstance(value, dict) and not value.get('module'):
            types = getattr(value, 'types', {})
            value = [{'name': k, 'type': typed('item', v, types.get(k)), 'value': v} for k, v in value.items()]
        elif getattr(value, 'types', None):
            self.fail(f'`{prop}` takes a value, not argument rows: a typed entry (`name: type = value`) belongs to a list of arguments', at)
        if isinstance(value, Args): value = dict(value)
        out = {'name': prop, 'type': typed(declared, value, written), 'value': value}
        if frozen: out['frozen'] = True
        return out

    def value(self):
        self.space()
        t, p = self.text, self.pos
        if p >= len(t): self.fail('expected a value')
        c = t[p]
        if c == '"':
            m = re.compile(r'"((?:[^"\\]|\\.)*)"').match(t, p)
            if not m: self.fail('a text is not closed')
            self.pos = m.end()
            return json.loads(m.group())
        if c == '%':
            m = VARIABLE.match(t, p)
            if not m and t.startswith('%?', p): self.fail('a `?` is still there: fill it with the value the step gives')
            if not m: self.fail('a %variable% is not closed')
            self.pos = m.end()
            return m.group()
        if c == '[' and re.compile(r'\[\s*(\{\s*)?[A-Za-z_]\w*\s*[=:]').match(t, p):
            self.fail('arguments are written as one dict: Parameter={name: "value", other: %x%}')
        if c == '[':
            self.pos += 1; items = []
            while not self.peek(']'):
                if self.at_end(): self.fail('a list is not closed: expected `]`')
                if items: self.take(',')
                items.append(self.value())
            self.take(']')
            return items
        if c == '{':
            # A dict literal ({"name": "a"}), or argument rows ({kind: text = "x"}): after `key:`, a
            # type followed by `=` makes the entry a typed row. Untyped entries read as either.
            self.pos += 1; d = Args()
            while not self.peek('}'):
                if self.at_end(): self.fail('a dict is not closed: expected `}`')
                if d: self.take(',')
                self.space()
                key = self.value() if self.text.startswith('"', self.pos) else self.ident()
                self.take(':')
                self.space()
                m = re.compile(r'(\w+(?:<\w+>)?)\s*(\?=|=)').match(self.text, self.pos)
                if m and m.group(1) not in ('true', 'false', 'null'):
                    d.types[key] = m.group(1)
                    self.pos = m.end()
                d[key] = self.value()
            self.take('}')
            return d
        m = re.compile(r'-?\d+(\.\d+)?(?![\w.])').match(t, p)
        if m:
            self.pos = m.end()
            return float(m.group()) if m.group(1) else int(m.group())
        for word, v in (('true', True), ('false', False), ('null', None)):
            if re.compile(rf'{word}\b').match(t, p): self.pos = p + len(word); return v
        if self.at_action(): return self.action()
        if (m := re.compile(r'[A-Za-z_][\w./-]*').match(t, p)):
            self.fail(f'a text is quoted: "{m.group()}"')
        if t[p] == '?': self.fail('a `?` is still there: fill it with the value the step gives')
        self.fail('expected a value: "text", a number, true, false, null, %variable%, [list], {dict} or an action')

def parse(text):
    """One step's formal (without its [i]) → its action rows, in the .pr's own shape."""
    r = _Reader(text)
    if r.at_end(): r.fail('a step holds at least one action')
    return r.actions()

STEP = re.compile(r'^\[(\d+)\][ \t]*', re.M)

def parse_answer(text):
    """A whole answer: `[i] …` per step, its continuation lines under it → {i: rows}."""
    heads = list(STEP.finditer(text))
    if not heads or text[:heads[0].start()].strip():
        raise FormalError('each step\'s line starts with its index: [0] action; action', text, 0)
    out = {}
    for n, h in enumerate(heads):
        end = heads[n + 1].start() if n + 1 < len(heads) else len(text)
        i = int(h.group(1))
        if i in out: raise FormalError(f'step [{i}] is answered twice', text, h.start())
        try:
            out[i] = parse(text[h.end():end].rstrip())
        except FormalError as e:
            # the position in the whole answer, not in the step
            raise FormalError(e.reason, text, h.end() + _offset(text[h.end():end], e)) from None
    return out

def _offset(text, e):
    lines = text.split('\n')
    return sum(len(l) + 1 for l in lines[:e.line - 1]) + e.col - 1

def is_formal(text):
    """A .goal step written in formal: the whole text reads as formal. It is parsed directly — no
    decider, no LLM."""
    try:
        parse(text)
        return True
    except FormalError:
        return False

# ---------------------------------------------------------------- writing
def _literal(v, t=None):
    if isinstance(v, bool): return 'true' if v else 'false'
    if v is None: return 'null'
    if isinstance(v, (int, float)): return repr(v)
    if isinstance(v, str):
        # A %variable% into a slot that is not text is the variable itself; anything else is a text.
        if VARIABLE.fullmatch(v) and (t or {}).get('name') != 'text': return v
        return json.dumps(v, ensure_ascii=False)
    if isinstance(v, list):
        if v and all(isinstance(r, dict) and 'name' in r and 'value' in r and not r.get('module') for r in v):
            return '{' + ', '.join(f'{_key(r["name"])}: {_literal(r["value"], r.get("type"))}' for r in v) + '}'
        return '[' + ', '.join(_literal(x) for x in v) + ']'
    if isinstance(v, dict):   # a dict literal: keys quoted, so it never reads as argument rows
        return '{' + ', '.join(f'{json.dumps(k, ensure_ascii=False)}: {_literal(x)}' for k, x in v.items()) + '}'
    raise TypeError(f'no formal for {v!r}')

def _key(k):
    return k if re.fullmatch(r'[A-Za-z_]\w*', k) else json.dumps(k)

def write_value(v, t=None, types=True):
    if isinstance(v, dict) and v.get('module'): return write_action(v, types)
    if isinstance(v, list) and v and all(isinstance(x, dict) and x.get('module') for x in v):
        return '[' + ', '.join(write_action(x, types) for x in v) + ']'
    if types and _rows(v):   # argument rows carry their types like any property: {kind: text = "x"}
        return '{' + ', '.join(f'{_key(r["name"])}: {face(r["type"])} = {_literal(r["value"], r.get("type"))}' for r in v) + '}'
    return _literal(v, t)

def _rows(v):
    return isinstance(v, list) and v and all(isinstance(r, dict) and 'name' in r and 'value' in r and not r.get('module') for r in v)

def _head(a, types=True):
    """module.name(Name: type = value, …) — every row with its type (types=False: the form the LLM
    and a .goal step write, which the parser types)."""
    def prop(name, t, op, value):
        return f'{name}: {t} {op} {value}' if types else f'{name}{" ?=" if op == "?=" else "="}{value}'
    props = [prop(r['name'], face(r['type']), '?=' if r.get('frozen') else '=', write_value(r['value'], r.get('type'), types))
             for r in a.get('property') or []]
    if a.get('recovery'):
        props.append(prop(RECOVERY, 'list<action>', '=', write_value(a['recovery'], None, types)))
    return f'{a["module"]}.{a["name"]}({", ".join(props)})'

def write_action(a, types=True):
    """One action; a condition's body inline in { }, and each modifier wrapping it, outermost first,
    the wrapped action on its own indented line."""
    s = _head(a, types)
    if a.get('child'):
        s += ' { ' + '; '.join(write_action(x, types) for c in a['child'] for x in c.get('action') or []) + ' }'
    for m in reversed(a.get('modifier') or []):
        s = f'{_head(m, types)} {{\n    ' + s.replace('\n', '\n    ') + '\n}'
    return s

def write_actions(actions, types=True):
    return '; '.join(write_action(a, types) for a in actions)

def write(actions, types=True):
    """One step's .pr rows → formal. The writer always writes types; types=False writes what the LLM
    answers and what a programmer may write in a .goal, which the parser types on reading."""
    return write_actions(actions, types)

def write_answer(steps, indent='    ', types=True):
    """{index: rows} → one `[i]` per step, its continuation lines indented under it."""
    return '\n'.join(f'[{i}] ' + write(rows, types).replace('\n', '\n' + indent) for i, rows in sorted(steps.items()))
