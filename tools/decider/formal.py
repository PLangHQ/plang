"""Formal — a step's actions written as calls, and the two directions between it and the .pr rows.

    [0] file.read(Path="orders/%orderId%.json"); variable.set(Name=%order%, Value=%!data%)
    [3] goal.call(Name="Compile")
        error.handle(RetryCount=2, Order="GoalFirst") { goal.call(Name="FixProperties") }

Grammar (ours, strict — the programmer's language has no syntax, this notation does):

    step      = ["[" index "]"] line { NEWLINE indent line }
    line      = action { ";" action }
                  a line holding a modifier wraps the action written before it (its host);
                  any other line continues the step's actions
    action    = module "." name "(" [ prop { "," prop } ] ")" [ body ]
    body      = "{" [ action { ";" action } ] "}"
                  a condition's body is its child; a modifier's body is its recovery
    prop      = Name "=" value
    value     = "text" | number | true | false | null | %variable% | list | dict | action
    list      = "[" [ value { "," value } ] "]"
    dict      = "{" [ key ":" value { "," key ":" value } ] "}"      key = name | "text"

A value's TYPE never comes from the answer: it is the property's declared type, or — where that is
`item`, the open slot — the literal's own (a quoted text is text, 5 a number, a %variable% item). A
dict given to a `list` property is its rows (goal.call's Parameter={to: %x%} → one row per key).

    parse(text) -> [action rows]         one step
    parse_answer(text) -> {index: rows}  a whole answer, one `[i]` line per step
    write(rows) -> text                  the .pr rows back to formal — the .pr's formal line
    is_formal(text) -> bool              a .goal step written in formal (parsed directly)
"""
import json, re
import build_pr as b

CONDITIONS = {('condition', 'if'), ('condition', 'elseif'), ('condition', 'else')}

class FormalError(Exception):
    """Where the formal stops making sense: line and column (1-based) and what was expected."""
    def __init__(self, message, text, pos):
        self.line = text.count('\n', 0, pos) + 1
        self.col = pos - (text.rfind('\n', 0, pos) + 1) + 1
        super().__init__(f'line {self.line}, column {self.col}: {message}')

# ---------------------------------------------------------------- the catalogue
def properties(module, name):
    """{Name: type} the action declares — the catalogue's rows plus the actions it holds as values."""
    props, _ = b.declared(module, name, held_actions=True)
    return {n: p for n, p in props.items()}

def is_modifier(module, name):
    return b.declared(module, name)[1]

def known(module, name):
    return b.handler_source(module, name) is not None

def typed(declared, value):
    """The row's type: the declared one, or for an open (`item`) slot the literal's own."""
    if declared and declared != 'item':
        m = re.fullmatch(r'(\w+)<(\w+)>', declared)
        return {'name': m.group(1), 'kind': m.group(2)} if m else {'name': declared}
    if isinstance(value, bool): return {'name': 'bool'}
    if isinstance(value, (int, float)): return {'name': 'number'}
    if isinstance(value, list): return {'name': 'list'}
    if isinstance(value, dict): return {'name': 'dict'}
    if value is None: return {'name': 'item'}
    return {'name': 'item'} if VARIABLE.fullmatch(value) else {'name': 'text'}

VARIABLE = re.compile(r'%[^%\s]+%')

# ---------------------------------------------------------------- reading
class _Reader:
    def __init__(self, text):
        self.text, self.pos = text, 0

    def fail(self, message):
        raise FormalError(message, self.text, self.pos)

    def space(self, newlines=False):
        while self.pos < len(self.text) and (self.text[self.pos] in ' \t\r' or (newlines and self.text[self.pos] == '\n')):
            self.pos += 1

    def peek(self, s):
        self.space()
        return self.text.startswith(s, self.pos)

    def take(self, s, newlines=False):
        self.space(newlines)
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

    # action = module "." name "(" props ")" [ body ]
    def action(self):
        start = self.pos
        module = self.ident(); self.take('.'); name = self.ident()
        if not known(module, name):
            self.pos = start; self.fail(f'`{module}.{name}` is not an action')
        declared = properties(module, name)
        self.take('(')
        rows = []
        while not self.peek(')'):
            self.space(True)
            if self.pos >= len(self.text): self.fail(f'`{module}.{name}(` is not closed: expected `)`')
            if rows: self.take(',', newlines=True)
            self.space(True)
            at = self.pos
            prop = self.ident()
            if prop not in declared:
                self.pos = at
                self.fail(f'`{module}.{name}` has no property `{prop}` (it has {", ".join(declared) or "none"})')
            if any(r['name'] == prop for r in rows):
                self.pos = at; self.fail(f'`{prop}` is given twice')
            self.take('=')
            rows.append(self.row(prop, declared[prop]))
            self.space(True)
        self.take(')')
        act = {'module': module, 'name': name, 'property': rows, 'modifier': []}
        if self.peek('{'):
            if not (is_modifier(module, name) or (module, name) in CONDITIONS):
                self.fail(f'`{module}.{name}` takes no body: only a condition (its child) and a modifier (its recovery) do')
            body = self.body()
            if is_modifier(module, name): act['recovery'] = body
            else: act['child'] = [{'text': write_actions(body), 'action': body}]
        return act

    def row(self, prop, spec):
        declared = spec['type']
        at = self.pos
        value = self.value(declared)
        if spec.get('options') and value not in spec['options']:
            self.pos = at
            self.fail(f'`{prop}` is one of {", ".join(spec["options"])}; `{value}` is not')
        if declared == 'variable' and not (isinstance(value, str) and VARIABLE.fullmatch(value)):
            self.pos = at; self.fail(f'`{prop}` names a variable: write it with its % signs')
        # A dict handed to a list property is its rows: Parameter={to: %x%} → [{name: to, …}].
        if declared.startswith('list') and isinstance(value, dict) and not value.get('module'):
            value = [{'name': k, 'type': typed('item', v), 'value': v} for k, v in value.items()]
        return {'name': prop, 'type': typed(declared, value), 'value': value}

    def body(self):
        self.take('{')
        actions = []
        while not self.peek('}'):
            if actions: self.take(';', newlines=True)
            self.space(True)
            actions.append(self.action())
            self.space(True)
        self.take('}')
        return actions

    def value(self, declared=None):
        self.space(True)
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
            if not m: self.fail('a %variable% is not closed')
            self.pos = m.end()
            return m.group()
        if c == '[':
            self.pos += 1; items = []
            while not self.peek(']'):
                if items: self.take(',', newlines=True)
                items.append(self.value())
                self.space(True)
            self.take(']')
            return items
        if c == '{':
            self.pos += 1; d = {}
            while not self.peek('}'):
                if d: self.take(',', newlines=True)
                self.space(True)
                key = self.value() if self.text.startswith('"', self.pos) else self.ident()
                self.take(':')
                d[key] = self.value()
                self.space(True)
            self.take('}')
            return d
        m = re.compile(r'-?\d+(\.\d+)?(?![\w.])').match(t, p)
        if m:
            self.pos = m.end()
            return float(m.group()) if m.group(1) else int(m.group())
        for word, v in (('true', True), ('false', False), ('null', None)):
            if re.compile(rf'{word}\b').match(t, p): self.pos = p + len(word); return v
        if self.at_action(): return self.action()
        self.fail('expected a value: "text", a number, true, false, null, %variable%, [list], {dict} or an action')

def _host(actions):
    """The action a modifier line wraps: the last action written before it."""
    return actions[-1] if actions else None

def parse(text):
    """One step's formal (without its [i]) → its action rows, in the .pr's own shape."""
    r = _Reader(text)
    actions = []
    first = True
    while True:
        r.space(True)
        if r.pos >= len(r.text): break
        line_start = r.text.rfind('\n', 0, r.pos) + 1
        new_line = first or r.text[line_start:r.pos].strip() == ''
        if not new_line:
            r.take(';')
            r.space(True)
        first = False
        r.space()
        at = r.pos
        act = r.action()
        if is_modifier(act['module'], act['name']):
            # A modifier is written on its own line, under the action it wraps.
            if not new_line or r.text[line_start:at].strip() or line_start == 0 or _host(actions) is None:
                r.pos = at; r.fail(f'`{act["module"]}.{act["name"]}` is a modifier: write it on the next line, under the action it wraps')
            _host(actions)['modifier'].append(act)
        else:
            actions.append(act)
        r.space()
        if r.pos < len(r.text) and r.text[r.pos] not in ';\n':
            r.fail('expected `;`, a new line or the end of the step')
    if not actions: r.fail('a step holds at least one action')
    return actions

STEP = re.compile(r'^\[(\d+)\][ \t]*', re.M)

def parse_answer(text):
    """A whole answer: `[i] …` lines, each step's continuation lines indented under it → {i: rows}."""
    heads = list(STEP.finditer(text))
    if not heads:
        raise FormalError('expected `[i]` at the start of a step', text, 0)
    lead = text[:heads[0].start()].strip()
    if lead: raise FormalError('text before the first `[i]` step', text, 0)
    out = {}
    for n, h in enumerate(heads):
        end = heads[n + 1].start() if n + 1 < len(heads) else len(text)
        i = int(h.group(1))
        if i in out: raise FormalError(f'step [{i}] is answered twice', text, h.start())
        try:
            out[i] = parse(text[h.end():end].rstrip())
        except FormalError as e:
            # Report the position in the whole answer, not in the step.
            raise FormalError(str(e).split(': ', 1)[1], text, h.end() + _offset(text[h.end():end], e)) from None
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
def write_value(v, t=None):
    if isinstance(v, dict) and v.get('module'): return write_action(v)
    if isinstance(v, bool): return 'true' if v else 'false'
    if v is None: return 'null'
    if isinstance(v, (int, float)): return repr(v)
    if isinstance(v, str):
        # A %variable% into a slot that is not text is the variable itself; anything else is a text.
        if VARIABLE.fullmatch(v) and (t or {}).get('name') != 'text': return v
        return json.dumps(v, ensure_ascii=False)
    if isinstance(v, list):
        if v and all(isinstance(r, dict) and 'name' in r and 'value' in r and not r.get('module') for r in v):
            return '{' + ', '.join(f'{_key(r["name"])}: {write_value(r["value"], r.get("type"))}' for r in v) + '}'
        return '[' + ', '.join(write_value(x) for x in v) + ']'
    if isinstance(v, dict):
        return '{' + ', '.join(f'{_key(k)}: {write_value(x)}' for k, x in v.items()) + '}'
    raise TypeError(f'no formal for {v!r}')

def _key(k):
    return k if re.fullmatch(r'[A-Za-z_]\w*', k) else json.dumps(k)

def write_action(a):
    props = ', '.join(f'{r["name"]}={write_value(r["value"], r.get("type"))}' for r in a.get('property') or [])
    s = f'{a["module"]}.{a["name"]}({props})'
    if a.get('child'):
        s += ' { ' + '; '.join(write_action(x) for c in a['child'] for x in c.get('action') or []) + ' }'
    if a.get('recovery'):
        s += ' { ' + '; '.join(write_action(x) for x in a['recovery']) + ' }'
    return s

def write_actions(actions):
    return '; '.join(write_action(a) for a in actions)

def write(actions, indent='    '):
    """One step's .pr rows → formal. An action that carries modifiers ends its line; each modifier
    follows on its own line, indented under it; the actions after it continue on the next line."""
    lines, current = [], []
    for a in actions:
        current.append(write_action(a))
        if a.get('modifier'):
            lines.append('; '.join(current)); current = []
            lines += [write_action(m) for m in a['modifier']]
    if current: lines.append('; '.join(current))
    return ('\n' + indent).join(lines)

def write_answer(steps):
    """{index: rows} → one `[i]` block per step, its continuation lines indented under it."""
    return '\n'.join(f'[{i}] ' + write(rows) for i, rows in sorted(steps.items()))
