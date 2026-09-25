"""Build .goal files into .pr files with the decider pipeline — the python stand-in for the plang
builder until the runtime can run the plang one.

    stage 1  decider noul    which modules each step uses
    stage 2  decider choice  which action of each such module
    stage 3  gpt-5.4-nano    order, count, property values — using os/system/builder/llm/Properties.llm
                             VERBATIM, so this exercises the real prompt, not a copy of it

Output goes to out/<goal file's folder>/.build/<name>.pr — a staging tree, never over the live
builder's .pr files, so a bad build cannot break anything before it has been read.

    python3 build_pr.py os/system/builder            # every .goal under a folder
    python3 build_pr.py os/system/builder/Build.goal  # one file
"""
import json, os, re, sys, glob, time, urllib.request
import harness as h

ROOT = h.ROOT
OUT = os.path.join(os.path.dirname(__file__), 'out')
OPENAI_KEY = os.environ['OPENAI_API_KEY']
MODEL = os.environ.get('STAGE3_MODEL', 'gpt-5.4-nano')
SYSTEM = open(f'{ROOT}/os/system/builder/llm/Properties.llm', encoding='utf-8').read()

# ---------------------------------------------------------------- the catalogue, from the C#
PROP = re.compile(r'public\s+partial\s+(?:global::app\.)?data\.@this(?:<(?P<type>.+?)>)?(?P<opt>\?)?\s+(?P<name>\w+)\s*\{\s*get;\s*init;\s*\}')

_sets = {}
def closed_set(cs_name):
    """(kind, options) of a closed set, read off its C# declaration: the name it declares with
    [PlangType("…")], and its enum members — or, for a named-set class, its Registry keys."""
    short = cs_name.split('.')[-1]
    if short in _sets: return _sets[short]
    for p in glob.glob(f'{ROOT}/PLang/app/**/*.cs', recursive=True):
        src = open(p, encoding='utf-8').read()
        m = re.search(rf'\[global::app\.Attributes\.PlangType\("(?P<kind>\w+)"\)\]\s*public\s+(?:sealed\s+)?(?P<what>enum|class)\s+{short}\b', src)
        if not m: continue
        if m.group('what') == 'enum':
            body = src[m.end():].split('{', 1)[1].split('}', 1)[0]
            body = re.sub(r'/\*.*?\*/', '', re.sub(r'//[^\n]*', '', body), flags=re.S)
            options = [re.sub(r'\s*=.*', '', v).strip() for v in body.split(',') if v.strip()]
        else:
            options = re.findall(r'\["([^"]+)"\]\s*=', src)
        _sets[short] = (m.group('kind'), options)
        return _sets[short]
    _sets[short] = (short.lower(), [])
    return _sets[short]

def plang_type(cs):
    """A C# slot type as its plang name. Generic arguments are stripped FIRST, so list<X> is list
    and never X; a bare Data slot is item. A choice is choice<kind>, its kind the set's own name."""
    if not cs: return 'item'
    outer = cs.split('<', 1)[0].strip()
    if 'choice' in outer:
        kind, _ = closed_set(cs.split('<')[-1].rstrip('>'))
        return f'choice<{kind}>'
    if 'goal.step.action.@this' in outer: return 'action'
    if 'variable' in outer.lower(): return 'variable'
    m = re.search(r'app\.type\.item\.@?(\w+)', outer)
    name = m.group(1) if m else None
    if name is None:
        # A global alias (`path`, `Goal`, `Step`) or a folder's @this (`app.goal.@this`): the plang
        # name is the alias itself, or the folder that owns the @this — lowercase, as the catalog names it.
        parts = [p for p in outer.replace('global::', '').split('.') if p]
        name = (parts[-2] if len(parts) > 1 and parts[-1] == '@this' else parts[-1]).lstrip('@').lower()
    if name == 'list' and '<' in cs:
        return f'list<{plang_type(cs.split("<", 1)[1].rsplit(">", 1)[0])}>'
    return name

def handler_source(module, action):
    for p in glob.glob(f'{ROOT}/PLang/app/module/action/{module}/*.cs'):
        src = open(p, encoding='utf-8').read()
        if re.search(rf'\[Action\("{re.escape(action)}"', src, re.I): return src
    return None

DEFAULT = re.compile(r'\[Default\((?P<value>.+?)\)\]')

def default_text(literal):
    """A [Default(…)] literal as the template prints it: a bool lowercase, a number as written, a
    text without its quotes, an enum member by its name."""
    v = literal.strip()
    if v in ('true', 'false'): return v
    if v.startswith('"') and v.endswith('"'): return v[1:-1]
    if re.fullmatch(r'-?\d+(\.\d+)?[dDfFmM]?', v): return v.rstrip('dDfFmM')
    return v.split('.')[-1]

# The catalog never shows these: host values, and the graph items the compiler injects itself.
NOT_ON_MENU = {'clr', 'goal', 'step', 'action', 'modifier'}

_decl = {}
def declared(module, action):
    """(properties, is_modifier) as the handler declares them — the same rows the plang catalog
    reflects (app/type/property/list/this.cs Reflect): infra-typed slots dropped, an IChannel
    action's synthetic `channel` row added last."""
    key = (module, action)
    if key in _decl: return _decl[key]
    src = handler_source(module, action) or ''
    lines = src.split('\n')
    props = {}
    for i, line in enumerate(lines):
        m = PROP.search(line)
        if not m: continue
        d = next((DEFAULT.search(l) for l in reversed(lines[max(0, i - 3):i]) if DEFAULT.search(l)), None)
        t = plang_type(m.group('type'))
        if t in NOT_ON_MENU: continue
        options = closed_set(m.group('type').split('<')[-1].rstrip('>'))[1] if t.startswith('choice<') else None
        props[m.group('name')] = {'type': t, 'options': options, 'nullable': bool(m.group('opt')),
                                  'default': default_text(d.group('value')) if d else None}
    if re.search(r'class\s+\w+\s*:[^{]*\bIChannel\b', src):
        props['channel'] = {'type': 'text', 'options': None, 'nullable': True, 'default': None}
    _decl[key] = (props, '[Modifier(' in src)
    return _decl[key]

def menu_row(name, p):
    """One property row exactly as propertiesUser.template writes it."""
    options = f': one of {", ".join(p["options"])}' if p.get('options') else ''
    required = not p['nullable'] and p['default'] is None
    default = f', default {p["default"]}' if p['default'] is not None else ''
    return f'{name} ({p["type"]}{options}, {"required" if required else "optional"}{default})'

# ---------------------------------------------------------------- .goal text -> goals
def parse(path):
    """A .goal file: a goal starts at a line that is not a step and not a comment; `- ` lines are
    its steps, `/ ` lines are comments on the step that follows."""
    goals, cur, comment = [], None, []
    for n, raw in enumerate(open(path, encoding='utf-8'), 1):
        line = raw.rstrip('\n')
        s = line.strip()
        if not s: continue
        if s.startswith('/'):
            comment.append(s[1:].strip()); continue
        if s.startswith('- '):
            if cur is None: continue
            # Indentation is structure — an indented step is the body of the one above — and the
            # tuned state shows it, so it is carried rather than flattened.
            indent = (len(line) - len(line.lstrip())) // 4
            cur['steps'].append({'index': len(cur['steps']), 'text': s[2:].strip(), 'indent': indent,
                                 'lineNumber': n, 'comment': '\n'.join(comment) or None})
            comment = []; continue
        cur = {'name': s, 'steps': []}; goals.append(cur); comment = []
    return goals

# ---------------------------------------------------------------- stages
PROMPTS = '/shared/coder/llm/plang'   # every prompt sent, per goal, readable

def dump_to(folder, label):
    """Point the harness's raw dump at this goal's folder under this stage's label."""
    os.makedirs(folder, exist_ok=True)
    h._local.dump = (folder, label)

def readable(folder, label):
    """Split a raw decider request into the state as plain text and the questions as JSON, and the
    response into its answers — the raw request buries the state as one escaped string."""
    req = os.path.join(folder, f'{label}.request.json')
    if not os.path.exists(req): return
    payload = json.load(open(req, encoding='utf-8'))
    state = payload['state']
    open(os.path.join(folder, f'{label}.state.txt'), 'w').write(
        state if isinstance(state, str) else json.dumps(state, indent=2, ensure_ascii=False))
    json.dump(payload['questions'], open(os.path.join(folder, f'{label}.questions.json'), 'w'),
              indent=2, ensure_ascii=False)
    os.remove(req)
    resp = os.path.join(folder, f'{label}.response.json')
    if os.path.exists(resp):
        json.dump(json.load(open(resp)).get('answers'), open(os.path.join(folder, f'{label}.answers.json'), 'w'),
                  indent=2, ensure_ascii=False)
        os.remove(resp)

def menu_for(goal, cat, folder=None):
    if folder: dump_to(folder, '1.decider')
    probs, *_ = h.stage1(goal, cat)
    if folder: readable(folder, '1.decider'); dump_to(folder, '2.decider')
    # `@store` is its own question — "does this step keep its result in a variable?" — so it never
    # reaches stage 2 as a module; it puts variable.set on the menu directly.
    chosen = {i: [m for m, p in probs[i].items() if m in cat and p is not None and p >= 0.5] for i in probs}
    acts, *_ = h.stage2(goal, cat, chosen)
    if folder: readable(folder, '2.decider'); h._local.dump = None
    menu = {}
    for s in goal['steps']:
        i = s['index']
        entries = [f'{m}.{acts[(i, m)][0]}' for m in chosen.get(i, []) if (i, m) in acts]
        if (probs.get(i, {}).get('@store') or 0) >= 0.5 and 'variable.set' not in entries:
            entries.append('variable.set')
        menu[i] = entries
    return menu, probs

def notes_block(module, action):
    """The action's notes (os/system/modules/<module>/<action>.notes.md) as the template prints them:
    under `notes:`, stripped, split on newlines (Liquid's split drops the blank lines), each indented."""
    path = f'{ROOT}/os/system/modules/{module}/{action}.notes.md'
    if not os.path.exists(path): return ''
    lines = [l for l in open(path, encoding='utf-8').read().strip().split('\n') if l != '']
    return '\n        notes:' + ''.join(f'\n          {l}' for l in lines)

def user_message(goal, menu):
    """The stage-3 user message, byte for byte what os/system/builder/llm/templates/propertiesUser.template
    renders — except menu order: the template walks the module catalog (hash order), this walks the menu."""
    out = '\n' + goal['name'] + '\n'
    steps = goal['steps']
    for n, s in enumerate(steps):
        out += f'\nstep {s["index"]}: {s["text"]}'
        # The steps indented under it (goal.step.list Body): consecutive followers written deeper.
        body = []
        for b in steps[n + 1:]:
            if b.get('indent', 0) <= s.get('indent', 0): break
            body.append(str(b['index']))
        if body:
            out += (f'\n   (its body is step{"s" if len(body) > 1 else ""} {", ".join(body)}, indented below'
                    ' — the builder places it; give this step no child)')
        out += '\n   menu:'
        for choice in menu.get(s['index'], []):
            module, action = choice.split('.', 1)
            props, _ = declared(module, action)
            out += f'\n     {choice}'
            for name, p in props.items():
                out += f'\n        {menu_row(name, p)}'
            out += notes_block(module, action)
        out += '\n'
    return out + '\n'

# The stage-3 answer shape — the file BuildGoal/Properties.goal passes as Schema, so python sends
# what plang sends. OpenAi.cs appends it to the system message; there is no response_format.
SCHEMA = open(f'{ROOT}/os/system/builder/llm/Properties.schema', encoding='utf-8').read()
SYSTEM_SENT = SYSTEM + '\n' + f'You MUST respond in JSON, schema: {SCHEMA}'

def properties(goal, menu, folder=None):
    user = user_message(goal, menu)
    if folder:
        open(os.path.join(folder, '3.llm.system.txt'), 'w').write(SYSTEM_SENT)
        open(os.path.join(folder, '3.llm.user.txt'), 'w').write(user)
    # The same request OpenAi.cs builds: temperature 0, max_completion_tokens 16000, the schema in the
    # system message.
    body = json.dumps({'model': MODEL, 'temperature': 0.0, 'max_completion_tokens': 16000,
                       'messages': [{'role': 'system', 'content': SYSTEM_SENT},
                                    {'role': 'user', 'content': user}]}).encode()
    req = urllib.request.Request('https://api.openai.com/v1/chat/completions', data=body,
                                 headers={'Authorization': f'Bearer {OPENAI_KEY}',
                                          'Content-Type': 'application/json'})
    with urllib.request.urlopen(req, timeout=300) as r:
        answer = json.loads(json.loads(r.read())['choices'][0]['message']['content'])
    if folder:
        json.dump(answer, open(os.path.join(folder, '3.llm.answer.json'), 'w'), indent=2, ensure_ascii=False)
    return answer

# ---------------------------------------------------------------- answer -> .pr
DEVIATIONS = []   # answers that left the shape the prompt asked for — a prompt-accuracy signal

def rows(raw, where):
    """The property rows of an answer. The prompt asks for [{"name", "value"}]; a {"Prop": value}
    dict, or a list of single-key dicts, is accepted but RECORDED — the model drifting back to the
    older shape is something the prompt has to fix, not something to absorb silently."""
    if isinstance(raw, dict):
        DEVIATIONS.append(f'{where}: property as a dict, not a list')
        return [{'name': k, 'value': v} for k, v in raw.items()]
    out = []
    for p in raw or []:
        if isinstance(p, dict) and 'name' in p: out.append(p)
        elif isinstance(p, dict) and len(p) == 1:
            DEVIATIONS.append(f'{where}: a row without "name" ({next(iter(p))})')
            k, v = next(iter(p.items())); out.append({'name': k, 'value': v})
        else:
            DEVIATIONS.append(f'{where}: unreadable row {p!r}')
    return out

def typed(row, fallback, where):
    """The row's type as the model gave it. A missing one is RECORDED (the .pr reader refuses an
    untyped row) and filled from the declaration so the rest of the goal still builds."""
    t = row.get('type')
    if not t:
        DEVIATIONS.append(f'{where}: row "{row.get("name")}" has no type')
        t = fallback
    if isinstance(t, str):
        m = re.fullmatch(r'(\w+)<(\w+)>', t)
        t = {'name': m.group(1), 'kind': m.group(2)} if m else {'name': t}
    return t

def pr_action(a, where=''):
    """One answered action in the .pr's own shape. Every row carries the type the model gave it —
    the .pr reader rejects a value slot that has none — and modifiers / recovery nest as given."""
    module, name = a.get('module'), a.get('name')
    if name is None and 'action' in a:
        DEVIATIONS.append(f'{where} {module}: element key "action", not "name"')
        name = a['action']
    where = f'{where} {module}.{name}'
    props, _ = declared(module, name)
    if 'parameter' in a and 'property' not in a:
        DEVIATIONS.append(f'{where}: key "parameter", not "property"')
    params = []
    for p in rows(a.get('property', a.get('parameter')), where):
        t = typed(p, (props.get(p['name']) or {}).get('type', 'item'), where)
        value = p.get('value')
        # An argument row (goal.call's Parameter) carries its own type, like any row.
        if isinstance(value, list) and all(isinstance(r, dict) and 'name' in r for r in value):
            value = [{**r, 'type': typed(r, 'item', f'{where} {p["name"]}')} for r in value]
        # A held action (a callback slot) is program: it is written in the action's own shape.
        if t.get('name') == 'action' and isinstance(value, dict): value = pr_action(value, where)
        params.append({'name': p['name'], 'type': t, 'value': value})
    out = {'module': module, 'name': name, 'property': params,
           'modifier': [pr_action(m, where) for m in a.get('modifier') or []]}
    if a.get('recovery'): out['recovery'] = [pr_action(r, where) for r in a['recovery']]
    # A condition's body: steps of its own, each {text, action}.
    if a.get('child'):
        out['child'] = [{'text': c.get('text', ''), 'action': [pr_action(x, f'{where} child') for x in c.get('action') or []]}
                        for c in a['child']]
    return out

def pr_goal(goal, answer, rel):
    """The answer is in the .pr's own keys — step, action, name, property, modifier, recovery. The
    older plural keys are read but RECORDED, never silently absorbed."""
    if 'steps' in answer: DEVIATIONS.append(f'{goal["name"]}: answer key "steps", not "step"')
    by_index = {}
    for e in answer.get('step', answer.get('steps', [])):
        if 'actions' in e: DEVIATIONS.append(f'{goal["name"]}[{e.get("index")}]: key "actions", not "action"')
        by_index[e.get('index')] = e.get('action', e.get('actions')) or []
    return {'name': goal['name'], 'path': '/' + rel,
            'step': [{'index': s['index'], 'text': s['text'], 'lineNumber': s['lineNumber'],
                      **({'comment': s['comment']} if s['comment'] else {}),
                      'waitForExecution': True,
                      'action': [pr_action(a, f'{goal["name"]}[{s["index"]}]')
                                 for a in by_index.get(s['index'], [])]}
                     for s in goal['steps']]}

def match(goal, answer):
    """What keeps the answer from lining up with the goal's steps — the same rule as the plang
    builder's build.match (goal.step.list Match): exactly one entry per step, in order, entry i
    labelled "index": i, none without actions. Empty when it matches."""
    entries = answer.get('step', answer.get('steps', [])) if isinstance(answer, dict) else []
    steps, problems = goal['steps'], []
    for i in range(max(len(steps), len(entries))):
        if i >= len(entries): problems.append(f'step {i} ("{steps[i]["text"]}") has no entry'); continue
        if i >= len(steps): problems.append(f'entry {i} is extra: the goal has {len(steps)} steps'); continue
        e = entries[i]
        if not isinstance(e, dict): problems.append(f'entry {i} is not an object'); continue
        if e.get('index') is None: problems.append(f'entry {i} has no index')
        elif e['index'] != i: problems.append(f'entry {i} is labelled index {e["index"]}')
        actions = e.get('action') or e.get('actions')
        if not actions: problems.append(f'step {i} ("{steps[i]["text"]}") has no actions'); continue
        # A step with steps indented under it gets its body from that layout (build.fold); a child
        # the answer wrote for it is the body written twice.
        body = []
        for b in steps[i + 1:]:
            if b.get('indent', 0) <= steps[i].get('indent', 0): break
            body.append(b['index'])
        if body and any(isinstance(a, dict) and a.get('child') for a in actions):
            problems.append(f"step {i}'s body is the indented steps below it "
                            f"({', '.join(f'step {n}' for n in body)}); leave its child empty")
        if broken := chain(actions, bool(body)):
            problems.append(f'step {i} ("{steps[i]["text"]}") — {broken}')
    return problems

def chain(actions, body_below):
    """The condition chain's shape in one step's actions — the same rule as the action list's Chain
    (goal/step/action/list/this.cs): from the first if on, only elseif/else may follow, each right after
    an if/elseif; every branch has a body. Empty when the chain is whole."""
    def is_cond(a): return isinstance(a, dict) and a.get('module') == 'condition' and a.get('name') in ('if', 'elseif', 'else')
    condition, missing = None, ''
    for n, a in enumerate(actions):
        if is_cond(a) and a['name'] != 'if':
            before = actions[n - 1] if n > 0 else None
            if not is_cond(before) or before['name'] == 'else':
                return f'ElseWithoutIf: an {a["name"]} must be in the same step as its if'
        if is_cond(a):
            if not missing and not a.get('child') and not body_below:
                missing = f'BodyMissing: the {a["name"]} has no body — what the step does when it holds goes in its child'
            condition = a
            continue
        if condition is not None:
            return (f'BodyBesideCondition: `{a.get("module")}.{a.get("name")}` is after the {condition["name"]}'
                    " — a branch's body goes in its child")
    return missing

# ---------------------------------------------------------------- run
def build_goal(g, rel, cat):
    """One goal: decider menu, then stage 3. Goals are independent, so these run in parallel."""
    if not g['steps']:
        # A goal of only comments has nothing to decide; it builds to an empty goal.
        return {'name': g['name'], 'path': '/' + rel, 'step': []}, None, f'{g["name"]:<24}   0 steps  (nothing to build)'
    folder = os.path.join(PROMPTS, rel[:-5], g['name'])
    t0 = time.time()
    menu, probs = menu_for(g, cat, folder)
    t1 = time.time()
    answer = properties(g, menu, folder)
    t2 = time.time()
    timing = f'{g["name"]:<24} {len(g["steps"]):>3} steps  {t2 - t0:5.1f}s   decider {t1 - t0:4.1f}s  llm {t2 - t1:4.1f}s'
    # A mismatched answer would hand one step another step's actions: reported, never written.
    if problems := match(g, answer):
        return None, {'menu': menu, 'answer': answer}, f'{timing}\n    ANSWER DOES NOT MATCH THE GOAL: ' + '; '.join(problems)
    return pr_goal(g, answer, rel), {'menu': menu, 'answer': answer}, timing

def write(rel, results):
    """A file's goals, in file order, as one .pr: the first goal is the root, the rest its children."""
    root = results[0][0]
    root['child'] = [r[0] for r in results[1:]]
    dest = os.path.join(OUT, os.path.dirname(rel), '.build', os.path.basename(rel)[:-5].lower() + '.pr')
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    json.dump(root, open(dest, 'w'), indent=1)
    # The raw menu + answer per goal, so a wrong .pr can be traced to the stage that made it wrong.
    json.dump({r[0]['name']: r[1] for r in results if r[1]}, open(dest + '.raw.json', 'w'), indent=1)
    return dest

if __name__ == '__main__':
    import concurrent.futures as cf
    target = os.path.join(ROOT, sys.argv[1] if len(sys.argv) > 1 else 'os/system/builder')
    files = [target] if target.endswith('.goal') else sorted(
        f for f in glob.glob(f'{target}/**/*.goal', recursive=True) if '/.build/' not in f)
    cat = h.catalogue()
    t0 = time.time()
    jobs = {}   # rel -> [future per goal, in file order]
    with cf.ThreadPoolExecutor(int(os.environ.get('WORKERS', 16))) as ex:
        for f in files:
            rel = os.path.relpath(f, ROOT)
            jobs[rel] = [ex.submit(build_goal, g, rel, cat) for g in parse(f)]
        for rel, futures in jobs.items():
            print(rel, flush=True)
            try:
                results = [fu.result() for fu in futures]
                for r in results: print('  ' + r[2])
                if any(r[0] is None for r in results):
                    print('  -> NOT WRITTEN: an answer does not match its goal (see above)')
                    continue
                print('  ->', os.path.relpath(write(rel, results), ROOT))
            except Exception as e: print('  FAILED:', type(e).__name__, e)
    print(f'\nwall clock: {time.time() - t0:.1f}s')
    print(f'shape deviations: {len(DEVIATIONS)}')
    for d in DEVIATIONS: print('  ', d)
