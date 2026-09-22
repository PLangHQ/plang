"""Stage 3 — parameters. Reads each action's declared parameters off the C# handler, offers the
candidate values taken from the step, and asks the decider one choice question per parameter.

Labels are the parameter values in the .pr. A value the step does not contain (a dict, a list of
objects, a computed expression) is not a choice at all and is expected to fall to the llm — those
are counted separately, never as a miss.
"""
import json, os, re, glob, sys, collections
import harness as h

ROOT = h.ROOT

# ---------------------------------------------------------------- the declared parameters
PROP = re.compile(r'public\s+partial\s+(?:global::)?(?:app\.)?data\.@this(?:<(?P<type>[^>]*(?:<[^>]*>)?[^>]*)>)?(?P<opt>\?)?\s+(?P<name>\w+)\s*\{\s*get;\s*init;\s*\}')
DEFAULT = re.compile(r'\[Default\(([^\]]*)\)\]\s*$')

def plang_type(raw):
    """The PLang name of a declared C# type.

    A plang type is a namespace ending in `@this`: `app.variable.@this` is `variable`,
    `app.type.item.@bool.@this` is `bool`. The generic must be taken apart BEFORE the namespace is,
    or the dot-split tears `@this<number.Overflow>` into pieces and leaves `Overflow>` behind.
    `choice<T>` surfaces under T's own name — it IS T's closed option set; `list<T>`/`dict<T>` keep
    their container.
    """
    raw = (raw or '').strip()
    if not raw: return 'item'
    if '<' in raw:
        head, _, inner = raw.partition('<')
        outer = plang_type(head)
        arg = plang_type(inner.rsplit('>', 1)[0])
        return f'{outer}<{arg}>' if outer in ('list', 'dict') else arg
    parts = [x for x in re.split(r'[.:]+', raw) if x]
    while parts and parts[-1].lstrip('@') == 'this': parts.pop()
    return (parts[-1].lstrip('@') if parts else 'item') or 'item'


def parameters(module, action):
    """Every parameter an action declares, with its plang-ish type and whether it is optional."""
    for path in (f'{ROOT}/PLang/app/module/action/{module}/{action}.cs',
                 f'{ROOT}/PLang/app/module/action/{module}/{action.lower()}.cs'):
        if os.path.exists(path): break
    else:
        hits = [p for p in glob.glob(f'{ROOT}/PLang/app/module/action/{module}/**/*.cs', recursive=True)
                if re.search(rf'\[Action\("{re.escape(action)}"', open(p, encoding='utf-8').read(), re.I)]
        if not hits: return None
        path = hits[0]
    src = open(path, encoding='utf-8').read()
    out = {}
    lines = src.split('\n')
    for i, line in enumerate(lines):
        m = PROP.search(line)
        if not m: continue
        # walk back only over attribute/comment lines — the moment another property or a blank
        # line intervenes the attribute belonged to it, not to this one.
        dm = None
        for l in reversed(lines[max(0, i - 4):i]):
            t = l.strip()
            if not t or t.startswith('///') or t.startswith('//'): continue
            if t.startswith('['):
                dm = DEFAULT.search(l)
                if dm: break
                continue
            break
        t = plang_type(m.group('type'))
        out[m.group('name')] = {'type': t, 'optional': bool(m.group('opt')) or dm is not None,
                                'default': dm.group(1).split('.')[-1].strip('"\'') if dm else None}
    return out

# ---------------------------------------------------------------- candidates from the step
QUOTED = re.compile(r"'([^']*)'|\"([^\"]*)\"")
VARIABLE = re.compile(r'%[^%\s]+%')
NUMBER = re.compile(r'(?<![\w%.])-?\d+(?:\.\d+)?(?![\w%])')

WORD = re.compile(r'(?<![%\w])[A-Za-z_][\w/]*(?![%\w])')

def candidates(text):
    """Every piece the step contains, offered as-is. No parsing of meaning — these are the
    pieces of the sentence, and the decider decides which piece is which parameter."""
    out = {}
    for m in QUOTED.finditer(text):
        v = m.group(1) if m.group(1) is not None else m.group(2)
        out[v] = f'the text "{v}" written in the step'
    for v in VARIABLE.findall(text):
        out[v] = f'the variable {v}'
    for v in NUMBER.findall(text):
        out.setdefault(v, f'the number {v} written in the step')
    for w in ('true', 'false'):
        if re.search(rf'\b{w}\b', text, re.I): out[w] = f'the value {w} written in the step'
    # Bare words too — a goal name, a key, a channel are written plain. Offering every word is
    # cheap and keeps this free of any judgement about which word means what.
    for w in WORD.findall(text):
        out.setdefault(w, f'the word {w} written in the step')
    return out

# A parameter whose type is a closed option set is not answered from the step's words — its
# options ARE the criteria. Read them off the C# enum the type names.
ENUM = re.compile(r'enum\s+(\w+)\s*\{(?P<body>[^}]*)\}', re.S)

# A named-set class declares its options as dictionary keys rather than as an enum.
NAMED_SET = re.compile(r'^\s*\["([^"]+)"\]\s*=', re.M)

def options(type_name):
    if not type_name or type_name in ('item', 'text', 'number', 'bool', 'path', 'list', 'dict'): return None
    for p in glob.glob(f'{ROOT}/PLang/app/module/action/**/{type_name}.cs', recursive=True):
        keys = NAMED_SET.findall(open(p, encoding='utf-8').read())
        if keys: return {k: f'the option {k}' for k in dict.fromkeys(keys)}
    for p in glob.glob(f'{ROOT}/PLang/app/module/action/**/*.cs', recursive=True) + \
             glob.glob(f'{ROOT}/PLang/app/type/**/*.cs', recursive=True):
        try: src = open(p, encoding='utf-8').read()
        except Exception: continue
        for m in ENUM.finditer(src):
            if m.group(1) != type_name: continue
            names = [re.sub(r'\s*=.*', '', x).strip() for x in m.group('body').split(',')]
            names = [n for n in names if re.fullmatch(r'\w+', n or '')]
            if names: return {n: f'the option {n}' for n in names}
    return None

# ---------------------------------------------------------------- ask
NONE = '__none__'

# Not decided from the step's words, so never asked:
#  Value — on a storing step it is always the previous action's result; the store question
#          already settled that, and `%!data%` is never written in the sentence.
#  Type  — derived from the value, not stated (except the explicit `as <type>` form, which
#          rides the value itself).
NOT_A_QUESTION = {'Value', 'Type'}

def ask_params(goal, cat, plan):
    """plan: {step_index: [(module, action)]} — what stages 1+2 decided."""
    state = h.state_for(goal, cat, modules={m for acts in plan.values() for m, _ in acts})
    qs = {}
    meta = {}
    for s in goal['steps']:
        cands = candidates(s['text'])
        if not cands: continue
        for module, action in plan.get(s['index'], []):
            decl = parameters(module, action)
            if not decl: continue
            for pname, pinfo in decl.items():
                if pname in NOT_A_QUESTION: continue
                crit = ({'true': 'yes', 'false': 'no'} if pinfo['type'] == 'bool'
                        else options(pinfo['type']) or dict(cands))
                crit[NONE] = ('none of these — the value is not written in this step'
                              if not pinfo['optional'] else
                              'none of these — this parameter is not given in this step')
                key = f's{s["index"]}_{module}.{action}_{pname}'
                qs[key] = {'type': 'choice',
                           'instructions': (f'Step {h.step_no(s)} of this goal is `{s["text"].strip()}`. It calls '
                                            f'`{module}.{action}`. Parameter `{pname}` is {"optional" if pinfo["optional"] else "required"}, '
                                            f'of type {pinfo["type"]}. Which of these is the value of `{pname}` in step {h.step_no(s)}?'),
                           'criteria': crit}
                meta[key] = (s['index'], module, action, pname)
    if not qs: return {}, 0.0, 0
    out = {}; secs = 0.0; n = 0
    keys = list(qs)
    for i in range(0, len(keys), 120):          # keep a request from getting silly
        chunk = {k: qs[k] for k in keys[i:i + 120]}
        resp, t, _ = h.ask(state, chunk)
        secs += t; n += len(chunk)
        for k, a in resp['answers'].items():
            out[meta[k]] = (a.get('choice'), a.get('confidence'))
    return out, secs, n

# ---------------------------------------------------------------- labels
def label_params(step):
    """{(module, action, ParamName): value} straight off the .pr."""
    out = {}
    pos = [0]
    def walk(a):
        m, an = a.get('module'), a.get('action') or a.get('name')
        n = pos[0]; pos[0] += 1
        # `defaults` are materialised into the .pr but are never written in the step — they are
        # what happens when nobody answers, so they are not a parameter question.
        for p in a.get('parameter') or a.get('parameters') or []:
            # `Type` is excluded: the .pr holds CLR names there (object, string, tstring), which
            # are not plang types and are a separate leak — not a parameter question.
            if p.get('name') == 'Type': continue
            out[(n, m, an, p.get('name'))] = p.get('value')
        for mod in a.get('modifier') or a.get('modifiers') or []: walk(mod)
        for c in a.get('child') or []:
            for ca in c.get('action') or c.get('actions') or []: walk(ca)
    for a in step.get('_raw', []): walk(a)
    return out


# A parameter whose type is a composite plang type (goal.call, llm.message, …) is filled as an
# object. Its shape is read off the type's own public properties so the model is never guessing.
SHAPE = re.compile(r'public\s+(?:required\s+)?(?P<t>[\w\.<>\?:@]+)\s+(?P<n>\w+)\s*\{\s*get;')

# Only a COMPOSITE value is filled as an object. Everything else is written plainly in the step —
# a variable as %name%, a path as its path, an actor by name — and showing its internals invites
# the model to answer with the internals.
COMPOSITE = {'GoalCall'}

# A composite's shape is spelled out rather than read off its C# properties: reflection gives
# `parameter: item`, which says nothing, and the model answered with the whole argument text as a
# string. What it needs is the literal shape of the value it must write.
SPELLED = {
    'GoalCall': '{ "name": the goal\'s name as text, "parameter": a list of { "name", "value" } — '
                'one per argument the step passes to that goal, omitted when it passes none }',
}

def shape(type_name):
    if type_name in SPELLED: return SPELLED[type_name]
    if type_name not in COMPOSITE: return None
    for p in (glob.glob(f'{ROOT}/PLang/app/**/{type_name}.cs', recursive=True) +
              glob.glob(f'{ROOT}/PLang/app/**/{type_name.lower()}/this.cs', recursive=True)):
        src = open(p, encoding='utf-8').read()
        if f'class {type_name}' not in src and f'class @this' not in src: continue
        out = []
        for m in SHAPE.finditer(src):
            t = m.group('t').split('.')[-1].replace('@this','').strip('<>? ')
            n = m.group('n')
            if n in ('Event','Action','PrPath'): continue      # runtime plumbing, never authored
            out.append(f'{n[0].lower()+n[1:]}: {t or "item"}')
        if out: return '{ ' + ', '.join(out) + ' }'
    return None
