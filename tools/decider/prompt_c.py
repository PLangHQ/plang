"""Prompt C — the decider's picks pre-filled in formal, the answer in formal, and the double check.

    system  os/system/builder/llm/Properties.llm (no schema: the answer is formal text)
    user    the goal as written, one line per step: the step, `=> decider:` its picks ≥ 0.5 with their scores,
            `=> formal:` the certain ones (≥ 0.9) pre-filled — `?` for a value to fill, known values filled
            (`write to %x%` → variable.set(Name=%x%, Value=%!data%)); then Types; then Settings (only when
            a step names a %!path% of a class of settings), then Keys (only when a step reads %!app.X["key"]%);
            then each listed action once (signature,
            description line, notes), and goal.call's definition when a listed action holds actions (an
            action-typed property, on.error's Recovery)

The check — the LLM and the decider must agree:
    refused   a pick ≥ 0.9 missing from the step, or an action the decider did not list for it
              (goal.call held as a value or a recovery is allowed: it is not the step's own action)
    warning   the step was built from a possible pick (0.5–0.9), or a disagreement was settled on retry
"""
import collections, copy, json, os, re
import build_pr as b
import formal as f
import harness as h
import variables as ref

ROOT = b.ROOT
SYSTEM_C = open(f'{ROOT}/os/system/builder/llm/Properties.llm', encoding='utf-8').read()
CERTAIN, POSSIBLE = 0.9, 0.5
WRITE_TO = re.compile(r'write to\s+(?=%)', re.I)
# The classes of settings as the C# classes are — each path's options, name, type and default as the builder
# shows them. A C# twin test (SettingCatalogTwinTests) writes the file from the classes.
SETTINGS = json.load(open(f'{ROOT}/os/system/builder/llm/settings.json', encoding='utf-8'))
SETTING_PATHS = {path.lower(): path for path in SETTINGS}

def setting_paths(v):
    """The dotted paths a setting's variable spells, shortest first (variable.Paths): its root without the !,
    then each member; a binding, an index or a method ends them."""
    root = v['code'][0]['variable']
    if not root.startswith('!'): return
    path = root[1:]
    yield path
    for hop in v['code'][1:]:
        name = hop.get('property')
        if name is None or name.startswith('!'): return
        path += '.' + name
        yield path

def settings_named(goal):
    """The classes of settings the goal's steps name, each once, in the order first named (step.Setting): a
    %!…% names the class its longest path is."""
    named = []
    for s in goal['steps']:
        for v in ref.parse(s['text']):
            found = [SETTING_PATHS[p.lower()] for p in setting_paths(v) if p.lower() in SETTING_PATHS]
            if found and found[-1] not in named: named.append(found[-1])
    return named

KEYS_LINE = '- %!app.goal["/show"]% is one goal by its key: one element of %!app.goal.list% (so too %!app.module["file"]%)'

def is_keyed(v):
    """Reads one of the app's by its key (variable.IsKeyed): !app, a concept, then an index."""
    code = v['code']
    return (len(code) > 2 and code[0].get('variable', '').lower() == '!app'
            and 'property' in code[1] and not code[1]['property'].startswith('!') and 'index' in code[2])

def keys_block(goal):
    """The user message's Keys: the one line that teaches %!app.X["key"]%, when a step reads one so (step.HasKey)."""
    keyed = any(is_keyed(v) for s in goal['steps'] for v in ref.parse(s['text']))
    return '\n\nKeys\n' + KEYS_LINE if keyed else ''

def settings_block(goal):
    """The user message's Settings: each class a step names, %!path%(option: type = default, …); none, nothing."""
    named = settings_named(goal)
    if not named: return ''
    return '\n\nSettings' + ''.join(
        f'\n- %!{path}%(' + ', '.join(o['name'] + ': ' + o['type'] + ('' if o['default'] is None else ' = ' + o['default'])
                                      for o in SETTINGS[path]) + ')'
        for path in named)

def destination(text):
    """The variable a `write to %x%` names, as written (pick.list Destination)."""
    m = WRITE_TO.search(text)
    found = ref.read(text, m.end()) if m else None
    return found['text'] if found else None

def is_certain(a, p, picks_i):
    """A listed action is certain at 0.90 or more — and, picked through its module, only when stage 1 was that sure
    of its module too (the C# pick.list Listing's Sure)."""
    shares = (picks_i.get('@module') or {}).get(a)
    return p >= CERTAIN and (shares is None or (shares.get(a.split('.', 1)[0]) or 0.0) >= CERTAIN)

def module_shares(step):
    """{action: {module: share}} for the actions a decided step picked through a module (stage 2): how sure stage 1
    was of each module stage 2 asked about — the prompt shows it beside the action (the C# listed.Module)."""
    asked = {m: step['modules'].get(m) or 0.0 for m in step.get('asked', [])}
    return {a: asked for a, v in step['pick'].items() if v['from'].startswith('stage 2')} if asked else {}

def listed(picks_i, text=''):
    """The step's picks shown to the LLM: ≥ 0.5, most certain first. A step that says `write to %x%`
    has variable.set whatever the decider scored it: that is a known value, not a guess. An unsure step
    adds the top 3 of the decider's popular-action choice that reach the choice floor (h.RUNNER_UP, 0.2:
    an option worth considering); below it an option is not listed."""
    out = {a: p for a, p in picks_i.items() if not a.startswith('@') and p is not None and p >= POSSIBLE}
    if destination(text) and 'variable.set' not in out: out['variable.set'] = picks_i.get('variable.set') or 0.0
    for a, p in popular(picks_i).items():
        if a not in out: out[a] = p
    return sorted(out.items(), key=lambda ap: -ap[1])

def popular(picks_i):
    """The popular-action choice's options that reach the choice floor."""
    return {a: p or 0.0 for a, p in (picks_i.get('@popular') or {}).items() if (p or 0.0) >= h.RUNNER_UP}

def popular_only(picks_i):
    """The actions an unsure step lists only because the popular-action choice ranked them."""
    return {a for a in popular(picks_i) if not ((picks_i.get(a) or 0) >= POSSIBLE)}

def is_unsure(picks_i, text=''):
    """The decider was unsure of the step: it asked the popular-action choice (harness.unsure)."""
    return bool(picks_i.get('@popular'))


def holds_actions(action):
    """Does this action take actions as a value — an action-typed property, or a list of them (on.error's Recovery)?"""
    module, name = action.split('.', 1)
    props, _ = b.declared(module, name)
    return any(p['type'] in ('action', 'list<action>') for p in props.values())

def prefill(action, text):
    """One pick as the formal line pre-fills it: its required properties as `?`, what the step already
    says filled — `write to %x%` → variable.set(Name=%x%, Value=%!data%). An optional property gets no hole: it
    is the LLM's to add when the step names it (a Recovery, a Parameter, a RetryCount), as the examples teach."""
    module, name = action.split('.', 1)
    props, _ = b.declared(module, name)
    if action == 'variable.set' and (d := destination(text)):
        return f'variable.set(Name={d}, Value=%!data%)'
    required = [f'{n}=?' for n, p in props.items() if not p['nullable'] and p['default'] is None]
    return f'{action}(' + ', '.join(required) + ')'

# The known code's words (goal/step/pick/list Code): a step's first variable, a foreach's `as` name, and
# a `%x% = ` ending in one literal (quoted text, a number, true/false) or one variable. The variables are
# the parser's (variables.py); the words around them are these.
AS = re.compile(r'\bas\s+%?([A-Za-z_]\w*)%?', re.I)
ASSIGNS = re.compile(r'\s*=(?!=)')
ASSIGNS_ONE = re.compile(r'\s*=\s*("(?:[^"\\]|\\.)*"|-?\d+(?:\.\d+)?|true|false|%\S+%)\s*$')

def first(text):
    """The step's first variable that names a value by members, as written (pick.list First)."""
    return next((v['text'] for v, _ in ref.placed(text) if ref.is_members(v)), None)

def target(text):
    """The variable an assignment writes: `%x% = …` (not `==`), as written (pick.list Target)."""
    return next((v['text'] for v, end in ref.placed(text) if ref.is_bare(v) and ASSIGNS.match(text, end)), None)

def assigned(text):
    """`%x% = ` ending in one literal or one bare variable: (the variable, its value) (pick.list Assigned)."""
    for v, end in ref.placed(text):
        m = ASSIGNS_ONE.match(text, end) if ref.is_bare(v) else None
        if not m: continue
        value = m.group(1)
        if value.startswith('%') and not ((n := ref.read(value, 0)) and n['text'] == value and ref.is_bare(n)): continue
        return v['text'], value
    return None

def bare_names(text):
    """The bare names the step's words write: %name% — not a setting, not a way in (step.Scope)."""
    return [v['code'][0]['variable'] for v in ref.parse(text) if ref.is_bare(v)]
CALLS = re.compile(r'\bcall\s+(/?[A-Za-z_][\w./]*)', re.I)   # a goal the step's words call (pick.list Calls)
QUOTED = re.compile(r'"(?:[^"\\]|\\.)*"|\'[^\']*\'')

def every_action(rows):
    """Every action in the rows, wherever it sits: the actions, what they hold, their recovery, their
    bodies (pick.list Every)."""
    out = []
    for a in rows:
        if not isinstance(a, dict): continue
        out.append(a)
        for r in a.get('property') or []:
            vals = r['value'] if isinstance(r['value'], list) else [r['value']]
            out += every_action([v for v in vals if isinstance(v, dict) and 'module' in v])
        for c in a.get('child') or []: out += every_action(c.get('action') or [])
    return out

def called_goals(rows):
    """Every goal.call Name in the rows, wherever the call sits."""
    return [str(r['value']).strip('"') for a in every_action(rows) if (a.get('module'), a.get('name')) == ('goal', 'call')
            for r in a.get('property') or [] if r['name'] == 'Name']

def known_code(certain, text):
    """The code a step's certain picks already know, as (action, binds): the build's walk reads it
    (goal.step.list.Scope). A foreach first, binding its item to its collection's element; a
    `set %x% = literal|%y%`; each other action leaving its return as %!data%; a write to %x% last."""
    code, write = [], destination(text)
    for a in sorted(certain, key=lambda a: 0 if b.is_loop(*a.split('.', 1)) else 1):   # a loop leads (action.loop)
        if b.declared(*a.split('.', 1))[1]: continue   # a clause binds nothing
        if a == 'loop.foreach':
            if (c := first(text)):
                item = m.group(1) if (m := AS.search(text)) else 'item'
                code.append(('foreach', c.strip('%'), item))
        elif a == 'variable.set':
            if not write and (m := assigned(text)): code.append(('set', m[0].strip('%'), m[1]))
        else: code.append(('data', b.returns(*a.split('.', 1))))
    if write: code.append(('set', write.strip('%'), '%!data%'))
    return code

CHAIN = {'condition.if': 0, 'condition.elseif': 1, 'condition.else': 2}

def link(action):
    """Where an action stands in a condition chain (action.Link): if 0, elseif 1, else 2, anything else after."""
    return CHAIN.get(action, 3)

def literal_type(value):
    return 'text' if value.startswith('"') else 'bool' if value in ('true', 'false') else 'number'

def scope(store, code, text):
    """One step at the build's walk over the scratch store (step.Scope): the variables its words name,
    typed as known before each of its actions (a name the step only writes is not shown; item is unknown)."""
    names, known = list(dict.fromkeys(bare_names(text))), {}
    # before each action — and once for a step whose code nothing knows yet
    for action in code or [None]:
        for n in names:
            if n not in known and store.get(n, 'item') != 'item': known[n] = store[n]
        if action is None: break
        if action[0] == 'data': store['!data'] = action[1]
        elif action[0] == 'set':
            _, name, value = action
            if value.startswith('%'):
                if value.strip('%') in store: store[name] = store[value.strip('%')]
            # a text with variables inside is unknown, as variable.set's Scope has it
            elif not re.search(r'%[^%\s]+%', value): store[name] = literal_type(value)
        else:
            _, collection, item = action
            t = store.get(collection, '')
            if t.startswith('list<'): store[item] = t[5:-1]
    return [(n, known[n]) for n in names if n in known]

class Line:
    """A step's pre-filled formal line (goal/step/pick/line): the step's actions in order, `?` where one is still
    to come; on a lone if, the if opens a body its step's other actions go into."""
    def __init__(self, nests):
        self.nests, self.line, self.head, self.body = nests, [], -1, None
        self.produced, self.kept, self.appended = False, [], 0

    def add(self, call, opens, produces=False):
        self.produced = self.produced or produces
        if opens and self.nests and self.body is None:
            self.line.append(call); self.head = len(self.line) - 1; self.body = []; return
        into = self.body if self.body is not None else self.line
        if into and into[0] == '?': into[0] = call
        else: into.append(call)

    def lead(self, call):
        self.line.insert(0, call)
        if self.head >= 0: self.head += 1

    def insert(self, call):
        into = self.body if self.body is not None else self.line
        if not into: into.append('?')
        into.insert(1, call)

    def keep(self, stands, follows):
        marker = f'\x01keep{len(self.kept)}'
        self.kept.append((marker, stands, follows))
        self.add(marker, False)

    def append(self, call):
        self.line.append(call)
        self.appended += 1

    def placed(self, calls, top):
        kept = {m: (s, f) for m, s, f in self.kept}
        if not self.produced: return [kept[c][0] if c in kept else c for c in calls]
        own = [c for c in calls if c not in kept]
        n = len(own) - (self.appended if top else 0)
        return own[:n] + [kept[c][1] for c in calls if c in kept] + own[n:]

    def written(self):
        line = self.placed(self.line, True)
        return '; '.join(f'{c} {{ {"; ".join(self.placed(self.body, False))} }}' if i == self.head and self.body else c
                         for i, c in enumerate(line))

def user_message_c(goal, picks):
    """picks: {step index: {action: score}} — every score the decider gave."""
    lines = []
    for s in goal['steps']:
        lines.append((s, f'[{s["index"]}] {"    " * s.get("indent", 0)}- {s["text"]}'))
    out = 'Goal, as written:\n  ' + goal['name']
    # store: the build walk's scratch variables, name → type — born with every store's own (variable/list)
    shown, store = [], {'Now': 'datetime', 'NowUtc': 'datetime', 'GUID': 'guid'}
    for s, line in lines:
        pad = ' ' * (len(f'[{s["index"]}] ') + 4 * s.get('indent', 0))
        for c in (s.get('comment') or '').split('\n') if s.get('comment') else []:
            out += f'\n  {pad}/ {c}'
        out += f'\n  {line}'
        if s.get('kept') or h.is_formal(s['text']):   # already built, or its own code: in the goal for context, not asked
            out += ' => cached'; continue
        step_picks = listed(picks.get(s['index'], {}), s['text'])
        known = destination(s['text'])
        pop = popular_only(picks.get(s['index'], {}))
        shares = picks.get(s['index'], {}).get('@module', {})
        decider = ', '.join(f'{a} {p:.2f}'
                            + (' (module ' + ', '.join(f'{m} {x:.2f}' for m, x in shares[a].items()) + ')' if a in shares else '')
                            + ('' if is_certain(a, p, picks.get(s['index'], {})) else ' (write to)' if a == 'variable.set' and known
                                              else ' (possible, popular)' if a in pop else ' (possible)')
                            for a, p in step_picks)
        # one line per step: the step as written, => its picks, => the pre-filled formal (after a multi-line
        # step's last line)
        out += f' => decider: {decider or "(nothing it is sure of)"}'
        # the certain picks pre-filled; a known value (write to) pre-fills its variable.set, last
        certain = sorted([a for a, p in step_picks if is_certain(a, p, picks.get(s['index'], {})) and not (a == 'variable.set' and known)],
                         key=link)   # a certain condition chain leads (action.Link); the rest by score
        # each certain pick takes its own place (action.Prefill, pick.line): a step action after the ones before
        # it (or where a clause left `?`), a clause right after the first action where the step's actions go; a
        # lone if — no elseif or else listed, nothing indented below the step — holds them in its { }
        chain = any(a in ('condition.elseif', 'condition.else') for a, _ in step_picks)
        steps = goal['steps']
        at = steps.index(s)
        below = at + 1 < len(steps) and steps[at + 1].get('indent', 0) > s.get('indent', 0)
        line = Line(sum(1 for a in certain if link(a) == 0) == 1 and not chain and not below)
        for a in certain:
            call = prefill(a, s['text'])
            if b.declared(*a.split('.', 1))[1]: line.insert(call)
            elif b.is_loop(*a.split('.', 1)): line.lead(call)
            elif b.is_keep(*a.split('.', 1)): line.keep(call, call.replace('Value=?', 'Value=%!data%'))
            else: line.add(call, link(a) == 0, link(a) == 3 and b.returns(*a.split('.', 1)) != 'item')   # a condition's verdict is never kept
        if known: line.append(prefill('variable.set', s['text']))
        if line.written(): out += ' => formal: ' + line.written()
        types = scope(store, known_code([a for a, p in step_picks if is_certain(a, p, picks.get(s['index'], {}))], s['text']), s['text'])
        if types: out += ' => types: ' + ', '.join(f'%{n}% {t}' for n, t in types)
        shown += [a for a, _ in step_picks]
    shown = list(dict.fromkeys(shown))
    if any(holds_actions(a) for a in shown) and 'goal.call' not in shown:
        shown.append('goal.call')   # a held action's definition, so the LLM can write it
    # Types: the literal types, then every type a shown action's properties use.
    faces = {t: None for t in ('text', 'number', 'bool', 'list', 'dict')}
    for a in shown:
        for p in b.declared(*a.split('.', 1))[0].values():
            faces.setdefault(p['type'], p.get('options'))
    out += '\n\nTypes' + ''.join('\n' + b.type_line(t, o) for t, o in faces.items())
    out += settings_block(goal) + keys_block(goal)
    for a in shown:
        module, name = a.split('.', 1)
        out += f'\n\n## {b.signature(a)}'
        if d := b.doc(module, name, 'description'): out += '\n' + d.split('\n')[0]
        if n := b.doc(module, name, 'notes'): out += '\n' + n.replace('## ', '### ')
    return out + '\n'

# ---------------------------------------------------------------- the check
def own_actions(rows):
    """The step's own actions: top level (its clauses among them), a condition's body — not what a
    property holds (channel.set's Goal, on.error's Recovery)."""
    out = []
    for a in rows:
        out.append(f'{a["module"]}.{a["name"]}')
        for c in a.get('child') or []: out += own_actions(c.get('action') or [])
    return out

def held_actions(rows):
    """Actions held as values: a property's action, a Recovery's actions (and what those hold)."""
    out = []
    def walk(a):
        for r in a.get('property') or []:
            v = r['value']
            for x in (v if isinstance(v, list) else [v]):
                if f.is_action(x): out.append(f'{x["module"]}.{x["name"]}'); walk(x)
        for c in a.get('child') or []:
            for x in c.get('action') or []: walk(x)
    for a in rows: walk(a)
    return out

NAMED = re.compile(r'\b([a-z]+)\.([A-Za-z_]+)\(')

def named(line):
    """The actions a line names (module.name( …), for a line that doesn't parse: its refusal still
    reports every problem it can see, not only the first one the parser stopped at."""
    return [f'{m}.{n}' for m, n in NAMED.findall(line) if f.known(m, n)]

def unlisted(i, used, picks_i, text=''):
    """The step's own actions the decider did not list for it — named with the step's list."""
    shown = dict(listed(picks_i, text))
    return [f'{a} isn\'t one of step {i}\'s actions ({", ".join(shown)})'
            for a in dict.fromkeys(used) if a not in shown]

def disagreements(i, rows, picks_i, text=''):
    """What the LLM and the decider disagree on in step i: (refusals, unsure)."""
    used = own_actions(rows)
    shown = dict(listed(picks_i, text))
    # a certain action may sit anywhere in the step: `on error set %x% = …` sets inside the recovery
    anywhere = set(used) | set(held_actions(rows))
    refused = [f'step {i} leaves out {a}, which the decider is certain of ({p:.2f})'
               for a, p in shown.items() if is_certain(a, p, picks_i) and a not in anywhere]
    refused += unlisted(i, used, picks_i, text)
    # an action listed only through the popular-action choice builds with a warning of its own
    popular = [a for a in dict.fromkeys(used) if a in popular_only(picks_i)]
    refused += [f'step {i} holds {a}, which is not listed; only goal.call may be held without being listed'
                for a in dict.fromkeys(held_actions(rows)) if a != 'goal.call' and a not in shown]
    # a recovery that runs the very action it wraps (the same action, the same values) is not a recovery
    def same(x, y):
        return (x['module'], x['name']) == (y['module'], y['name']) and \
               [(r['name'], r['value']) for r in x.get('property') or []] == [(r['name'], r['value']) for r in y.get('property') or []]
    action = None
    for a in rows:
        if not f.is_clause(a['module'], a['name']): action = a; continue
        held = [x for r in a.get('property') or [] for x in (r['value'] if isinstance(r['value'], list) else [r['value']])]
        if action and any(same(x, action) for x in held if f.is_action(x)):
            refused.append(f'step {i}: the Recovery of {a["module"]}.{a["name"]} runs {action["module"]}.{action["name"]}, the action it is a clause of — '
                           'Recovery holds what the step runs on error')
    # the variable the step's words write (write to %x%, or %x% = … on a step that tests no condition —
    # there `=` compares) is written by a variable.set of the step's own code (pick.list Writes)
    tests = (picks_i.get('condition.if') or 0) >= POSSIBLE
    writes = destination(text) or (not tests and target(text))
    if writes:
        name = writes.strip('%').lower()
        if not any(a['module'] == 'variable' and a['name'] == 'set' and any(
                r['name'] == 'Name' and str(r['value']).strip('%"').lower() == name for r in a.get('property') or [])
                for a in every_action(rows)):
            refused.append(f'step {i} says it writes {writes}, but no action writes it: '
                           f'end the step with variable.set(Name={writes}, Value=%!data%)')
    # every goal the step's words call (`call X`, quoted texts left out) is called by the code, wherever
    # the goal.call sits (pick.list Calls)
    called = ['/' + n.lstrip('/') for n in called_goals(rows)]
    for goal in dict.fromkeys(m.group(1).rstrip('.,') for m in CALLS.finditer(QUOTED.sub('', text))):
        wanted = '/' + goal.lstrip('/')
        if wanted.lower().endswith('.goal'): wanted = wanted[:-5]
        if not any(n.lower().endswith(wanted.lower()) or wanted.lower().endswith(n.lower()) for n in called):
            refused.append(f'step {i} calls {goal}, but no action calls it')
    # unsure = built from a possible pick; a pick the known-value rule placed (write to → variable.set)
    # is not a guess, so it carries no warning
    known = {'variable.set'} if destination(text) else set()
    warnings = [f'step {i} uses {a}, which the decider was not sure of ({shown[a]:.2f})'
                for a in dict.fromkeys(used) if a in shown and not is_certain(a, shown[a], picks_i) and a not in known and a not in popular]
    warnings += [f'step {i} uses {a} from the decider\'s popular-action choice ({shown[a]:.2f}): unsure' for a in popular]
    return refused, warnings

def fold(goal, parsed):
    """A step with steps indented under it gets its body from that layout (build.fold). A child the
    answer wrote over it whose actions all come from the indented steps — all of them, or some — is a
    copy: dropped. A child holding anything else is invented: refused. Returns {i: refusal}; the
    copies are removed in place."""
    refused = {}
    steps = goal['steps']
    for i, rows in parsed.items():
        body = b.body_of(steps, i) if i < len(steps) else []
        if not body: continue
        below = collections.Counter(a for n in body for a in own_actions(parsed.get(n, [])))
        for a in rows:
            if not a.get('child'): continue
            inside = collections.Counter(x for c in a['child'] for x in own_actions(c.get('action') or []))
            if not inside - below: a.pop('child')
            else: refused.setdefault(i, []).append(
                f'step {i}\'s body is the steps indented under it ({", ".join(str(n) for n in body)}); '
                f'the builder places them — write step {i} without {{ }} and each indented step on its own line')
    return refused

# A condition operand is a value to compare: `Right=null` asks "== null", an absent Right asks nothing.
NULL_IS_A_VALUE = {('condition', 'if', 'Right'), ('condition', 'elseif', 'Right'), ('condition', 'compare', 'Right'),
                   ('condition', 'if', 'Left'), ('condition', 'elseif', 'Left'), ('condition', 'compare', 'Left')}

def drop_nulls(rows):
    """An explicit null on an optional property means absent: the row is dropped (its default applies,
    the same behaviour). A null that is a real value — a condition's operand — stays."""
    for a in rows:
        props, _ = b.declared(a['module'], a['name'])
        a['property'] = [r for r in a.get('property') or []
                         if not (r['value'] is None and r['name'] in props
                                 and (props[r['name']]['nullable'] or props[r['name']]['default'] is not None)
                                 and (a['module'], a['name'], r['name']) not in NULL_IS_A_VALUE)]
        for r in a['property']:
            v = r['value']
            for x in (v if isinstance(v, list) else [v]):
                if f.is_action(x): drop_nulls([x])
        for c in a.get('child') or []: drop_nulls(c.get('action') or [])

def is_default(value, default, declared):
    """Does the value equal the property's declared default (as the catalogue prints it)?"""
    if default is None: return False
    if isinstance(value, bool): return str(value).lower() == default.lower()
    if isinstance(value, (int, float)):
        try: return float(value) == float(default)
        except ValueError: return False
    if isinstance(value, str):
        if declared == 'variable': return value.strip('%') == default.strip('%')
        return value == default
    return False

def drop_defaults(rows):
    """S1: a property whose value equals its declared default is dropped — the default applies, the same
    behaviour — so a spelled-out default (Item=%item%, Overflow="Promote") is no longer a difference."""
    for a in rows:
        props, _ = b.declared(a['module'], a['name'])
        a['property'] = [r for r in a.get('property') or []
                         if not (r['name'] in props and is_default(r['value'], props[r['name']]['default'], props[r['name']]['type']))]
        for r in a['property']:
            v = r['value']
            for x in (v if isinstance(v, list) else [v]):
                if f.is_action(x): drop_defaults([x])
        for c in a.get('child') or []: drop_defaults(c.get('action') or [])

def uncovered(text, rows):
    """The %variables% the step's text writes that its answer doesn't hold anywhere — a value, a Name,
    inside a quoted text. %x% is plang's own marker, so this reads no human language."""
    written = f.write_actions(rows, types=False)
    return [v for v in dict.fromkeys(x['text'] for x in ref.parse(text)) if v not in written]

# A quoted literal in a step's text — "…" or '…' (a single quote only when not inside a word, so
# `don't` is not one). Quotes are the programmer's literal marker, not a human language.
LITERAL = re.compile(r'"([^"]*)"|(?<!\w)\'([^\']*)\'(?!\w)')

def values_of(rows):
    """Every text the answer holds — each value, each dict key and argument name — however deep."""
    out = []
    def walk(v):
        if isinstance(v, dict):
            for k, x in v.items():
                if k not in ('module', 'name', 'type', 'property', 'child', 'action', 'frozen'): out.append(str(k))
                walk(x)
        elif isinstance(v, list):
            for x in v: walk(x)
        elif isinstance(v, str): out.append(v)
    for a in rows:
        for r in a.get('property') or []: walk(r['value']); out.append(r['name'])
        for c in a.get('child') or []: out += values_of(c.get('action') or [])
    return out

def uncovered_literals(text, rows):
    """The quoted literals of the step's text that no value of its answer holds."""
    held = values_of(rows)
    lits = [m.group(1) if m.group(1) is not None else m.group(2) for m in LITERAL.finditer(text)]
    return [l for l in dict.fromkeys(lits) if l and not any(l in written(v) for v in held)]

def doubled(literal, rows):
    """The answer doubles the literal's backslashes (\\\\n for the step's \\n) — step.Cover names it."""
    return '\\' in literal and any(literal.replace('\\', '\\\\') in written(v) for v in values_of(rows))

QUOTED = re.compile(r'"(?:[^"\\]|\\.)*"')

def uncovered_numbers(text, rows):
    """The numbers the step writes as digits (outside its quoted texts) that its answer doesn't (step.Cover):
    present when the digits stand in the answer on their own, not inside a larger number — a duration's PT5S
    holds the step's 5."""
    answer = f.write(rows, types=False)
    return [n for n in dict.fromkeys(NUMBER.findall(QUOTED.sub('', text)))
            if not re.search(rf'(?<![\d.]){re.escape(n)}(?![\d.]|\.\d)', answer)]

def written(value):
    """A value as formal writes it, escapes and all — what the step's words are compared with (C#'s
    Cover reads the formal writer's text): the step's `\\n` is a new line, written `\\n`; a value that
    holds a backslash and an n is written `\\\\n` and is not it."""
    return json.dumps(value, ensure_ascii=False)[1:-1] if isinstance(value, str) else str(value)

NUMBER = re.compile(r'(?<![\w.])-?\d+(?:\.\d+)?(?![\w.])')

def unwritten(i, text, rows):
    """The numbers the answer writes that the step's words don't write as digits (step.Unwritten), each with
    the action and property writing it: {step, action, property, value, text, id}. The words may still give
    one in any language ("retry once"): the decider is asked (confirm_questions); only digits are read here.
    Read after S1, so a dropped default doesn't count."""
    said = {float(n) for n in NUMBER.findall(text)}
    out = []

    def nums(v):
        if isinstance(v, bool): return []
        if isinstance(v, (int, float)): return [v]
        if isinstance(v, dict): return [n for x in v.values() for n in nums(x)]
        if isinstance(v, list): return [n for x in v for n in nums(x)]
        return []

    def written(n):   # as the formal writer writes a number
        return str(int(n)) if float(n).is_integer() else repr(float(n))

    def walk(actions):
        for a in actions:
            if not isinstance(a, dict): continue
            for r in a.get('property') or []:
                v = r['value']
                held = [v] if f.is_action(v) else v if isinstance(v, list) and v and all(f.is_action(x) for x in v) else None
                if held is not None: walk(held); continue
                for n in nums(v):
                    if float(n) in said: continue
                    action, value = f'{a["module"]}.{a["name"]}', written(n)
                    u = {'step': i, 'action': action, 'property': r['name'], 'value': value, 'text': text,
                         'id': f's{i}_{action}.{r["name"]}={value}'}
                    if u not in out: out.append(u)
            for c in a.get('child') or []: walk(c.get('action') or [])
    walk(rows)
    return out

def given(u, confirmed):
    """Whether the decider's answer says the step's words give the number (unwritten.Given)."""
    return ((confirmed.get(u['id']) or {}).get('noul') or 0) >= 0.5

def confirm_state(numbers):
    """The decider's state for confirming numbers — byte for byte os/system/builder/llm/templates/confirm.state.template."""
    out = ('A plang step is a sentence saying what to do, written in any human language. Its code is a list of actions, each\n'
           'with properties. The code of each step below writes a number the step does not write as digits. A step can still\n'
           'give that number in words, in any language ("once" gives 1, "twice" 2, "a minute" 60 seconds); or the number is\n'
           'not in the step at all, and was invented.\n\nThe steps:')
    seen = []
    for n in numbers:
        if n['step'] in seen: continue
        seen.append(n['step']); out += f'\n- step {n["step"]}: {n["text"]}'
    out += '\n\nThe actions named:'
    named = []
    for n in numbers:
        if n['action'] in named: continue
        named.append(n['action'])
        module, action = n['action'].split('.', 1)
        d = f'{b.ROOT}/os/system/modules/{module}/{action}'
        about = open(d + '.description.md', encoding='utf-8').read().strip() if os.path.exists(d + '.description.md') else ''
        out += f'\n- {n["action"]}: {about}'
        if os.path.exists(d + '.notes.md'): out += f'\n  {open(d + ".notes.md", encoding="utf-8").read().strip()}'
    return out

def confirm_questions(numbers):
    """The decider's questions for confirming numbers, one noul per number under its id (confirm.template)."""
    return {n['id']: {'type': 'noul',
                      'instructions': f'Step {n["step"]} is `{n["text"]}`. Its code writes {n["action"]}({n["property"]}={n["value"]}). '
                                      f'Does step {n["step"]} give {n["property"]} the value {n["value"]}?',
                      'criteria': {'true': "the step's words give that value, in any wording or language",
                                   'false': "the step's words don't give it: the value is invented"}}
            for n in numbers}

def texts_of(rows):
    """Every text-typed property value the answer writes — the actions, the actions they hold, their
    clauses' recovery, their bodies (step.Texts)."""
    out = []
    for a in rows:
        for r in a.get('property') or []:
            v = r.get('value')
            if isinstance(v, dict) and 'module' in v: out += texts_of([v]); continue
            if isinstance(v, list) and v and all(f.is_action(x) for x in v): out += texts_of(v); continue
            if (r.get('type') or {}).get('name') == 'text' and isinstance(v, str): out.append(v)
        for c in a.get('child') or []: out += texts_of(c.get('action') or [])
    return out

def invented_texts(text, rows):
    """The texts the answer writes that the step's text doesn't hold — an invented value
    (channel="X" on a step that names no X). A choice's option, a number, a dict's keys are not texts."""
    return [t for t in dict.fromkeys(texts_of(rows)) if t and t.lower() not in text.lower()]

WHOLE = ('has no entry', 'is extra', 'is labelled', 'has no index', 'is not an object')

def check(goal, picks, parsed, confirmed=None):
    """(whole-answer refusals, {step: refusals}, warnings, numbers to confirm) of a parsed formal answer {i: rows}.
    A number the step's words don't write as digits (unwritten) is the decider's to confirm: with its
    `confirmed` answers, one it denies is refused as invented; without, when those numbers are the only
    problems they are returned to be asked (build.match's UnwrittenNumber), else refused with the rest.
    Whole: the answer doesn't line up with the goal (an extra or renumbered entry). Per step: no entry,
    no actions, the chain rule, a body over indented steps that isn't a copy (fold), the agreement
    with the decider, a %variable% of the step's text missing from its answer. Nulls on optional
    properties are dropped first."""
    written = copy.deepcopy(parsed)   # coverage reads the answer as written, before normalization
    for rows in parsed.values(): drop_nulls(rows); drop_defaults(rows)
    per_step = fold(goal, parsed)
    steps = goal['steps']
    # a step the answer leaves out is that step's refusal, as step.list.Read has it — the others stand
    for i in range(len(steps)):
        if i not in parsed: per_step.setdefault(i, []).append(f'step {i} ("{steps[i]["text"]}") has no entry')
    whole = [f'entry {i} is extra: the goal has {len(steps)} steps' for i in parsed if i >= len(steps)]
    warnings, pending = [], []
    for i, rows in parsed.items():
        if i >= len(steps): continue
        problems = per_step.setdefault(i, [])
        if not rows: problems.append(f'step {i} ("{steps[i]["text"]}") has no actions')
        elif broken := b.chain(rows, bool(b.body_of(steps, i))): problems.append(f'step {i} ("{steps[i]["text"]}") — {broken}')
        # a step written in formal was asked of no decider: there are no picks to agree with
        r, w = ([], []) if h.is_formal(steps[i]['text']) else disagreements(i, rows, picks.get(i, {}), steps[i]['text'])
        problems += r; warnings += w
        problems += [f'step {i}: {v} is in the step but not in your answer' for v in uncovered(steps[i]['text'], written[i])]
        # and the other way: a variable the answer names that the step's words don't is invented (step.Cover)
        problems += [f'step {i}: {v} isn\'t in the step — use only the step\'s variables'
                     for v in dict.fromkeys(re.findall(r'%[^%\s]+%', f.write(written[i], types=False)))
                     if not v.startswith('%!') and v not in steps[i]['text']]
        problems += [f'step {i}: "{l}" is in the step, and your answer doubles its backslashes — write each escape as the step does'
                     if doubled(l, written[i]) else f'step {i}: "{l}" is in the step but not in your answer'
                     for l in uncovered_literals(steps[i]['text'], written[i])]
        problems += [f'step {i}: {n} is in the step but not in your answer' for n in uncovered_numbers(steps[i]['text'], written[i])]
        for u in unwritten(i, steps[i]['text'], rows):
            if confirmed is None: pending.append(u)
            elif not given(u, confirmed):
                problems.append(f'step {i}: your answer writes {u["value"]}, which the step doesn\'t — leave out what the step doesn\'t give')
        problems += [f'step {i}: your answer writes "{t}", which the step doesn\'t — leave out what the step doesn\'t give'
                     for t in invented_texts(steps[i]['text'], rows)]
    refused = {i: p for i, p in per_step.items() if p}
    if pending and (whole or refused):   # among other problems, an unconfirmed number is refused with them
        for u in pending:
            refused.setdefault(u['step'], []).append(
                f'step {u["step"]}: your answer writes {u["value"]}, which the step doesn\'t — leave out what the step doesn\'t give')
        pending = []
    return whole, refused, warnings, pending
