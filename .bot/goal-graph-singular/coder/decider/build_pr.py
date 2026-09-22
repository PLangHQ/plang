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

def plang_type(cs):
    """A C# slot type as its plang name. Generic arguments are stripped FIRST, so list<X> is list
    and never X; a bare Data slot is item."""
    if not cs: return 'item'
    outer = cs.split('<', 1)[0].strip()
    if 'GoalCall' in outer: return 'goal.call'
    if 'variable' in outer.lower(): return 'variable'
    m = re.search(r'app\.type\.item\.@?(\w+)', outer)
    return m.group(1) if m else 'item'

def handler_source(module, action):
    for p in glob.glob(f'{ROOT}/PLang/app/module/action/{module}/*.cs'):
        src = open(p, encoding='utf-8').read()
        if re.search(rf'\[Action\("{re.escape(action)}"', src, re.I): return src
    return None

_decl = {}
def declared(module, action):
    """(properties, is_modifier) as the handler declares them."""
    key = (module, action)
    if key in _decl: return _decl[key]
    src = handler_source(module, action) or ''
    lines = src.split('\n')
    props = {}
    for i, line in enumerate(lines):
        m = PROP.search(line)
        if not m: continue
        has_default = any('[Default' in l for l in lines[max(0, i - 3):i])
        props[m.group('name')] = {'type': plang_type(m.group('type')),
                                  'optional': bool(m.group('opt')) or has_default}
    _decl[key] = (props, '[Modifier(' in src)
    return _decl[key]

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
    h.DUMP = (folder, label)

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
    if folder: readable(folder, '2.decider'); h.DUMP = None
    menu = {}
    for s in goal['steps']:
        i = s['index']
        entries = [f'{m}.{acts[(i, m)][0]}' for m in chosen.get(i, []) if (i, m) in acts]
        if (probs.get(i, {}).get('@store') or 0) >= 0.5 and 'variable.set' not in entries:
            entries.append('variable.set')
        menu[i] = entries
    return menu, probs

def user_message(goal, menu):
    out = [goal['name'], '']
    for s in goal['steps']:
        out.append(f'step {s["index"]}: {s["text"]}')
        out.append('   menu:')
        for choice in menu.get(s['index'], []):
            module, action = choice.split('.', 1)
            props, is_mod = declared(module, action)
            out.append(f'     {choice}' + ('  [modifier]' if is_mod else ''))
            for name, p in props.items():
                out.append(f'        {name} ({p["type"]}, {"optional" if p["optional"] else "required"})')
        out.append('')
    return '\n'.join(out)

def properties(goal, menu, folder=None):
    user = user_message(goal, menu)
    if folder:
        open(os.path.join(folder, '3.llm.system.txt'), 'w').write(SYSTEM)
        open(os.path.join(folder, '3.llm.user.txt'), 'w').write(user)
    body = json.dumps({'model': MODEL, 'response_format': {'type': 'json_object'},
                       'messages': [{'role': 'system', 'content': SYSTEM},
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
        DEVIATIONS.append(f'{where}: parameter as a dict, not a list')
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

def pr_action(a, where=''):
    """One answered action in the .pr's own shape. Every top-level property gets its DECLARED type
    — the .pr reader rejects a value slot that has none — and modifiers / recovery nest as given."""
    module, name = a.get('module'), a.get('action') or a.get('name')
    where = f'{where} {module}.{name}'
    props, _ = declared(module, name)
    params = []
    for p in rows(a.get('parameter'), where):
        t = p.get('type') or {'name': (props.get(p['name']) or {}).get('type', 'item')}
        if isinstance(t, str): t = {'name': t}
        params.append({'name': p['name'], 'type': t, 'value': p.get('value')})
    out = {'module': module, 'name': name, 'parameter': params,
           'modifier': [pr_action(m, where) for m in a.get('modifier') or []]}
    if a.get('recovery'): out['recovery'] = [pr_action(r, where) for r in a['recovery']]
    return out

def pr_goal(goal, answer, rel):
    by_index = {e.get('index'): e.get('actions') or [] for e in answer.get('steps', [])}
    return {'name': goal['name'], 'path': '/' + rel,
            'step': [{'index': s['index'], 'text': s['text'], 'lineNumber': s['lineNumber'],
                      **({'comment': s['comment']} if s['comment'] else {}),
                      'waitForExecution': True,
                      'action': [pr_action(a, f'{goal["name"]}[{s["index"]}]')
                                 for a in by_index.get(s['index'], [])]}
                     for s in goal['steps']]}

# ---------------------------------------------------------------- run
def build(path):
    cat = h.catalogue()
    rel = os.path.relpath(path, ROOT)
    goals = parse(path)
    built, raw = [], {}
    for g in goals:
        if not g['steps']:
            # A goal of only comments has nothing to decide; it builds to an empty goal.
            built.append(({'name': g['name'], 'path': '/' + rel, 'step': []}, {}))
            print(f'  {g["name"]:<24}   0 steps  (nothing to build)', flush=True)
            continue
        folder = os.path.join(PROMPTS, rel[:-5], g['name'])
        t0 = time.time()
        menu, probs = menu_for(g, cat, folder)
        t1 = time.time()
        answer = properties(g, menu, folder)
        t2 = time.time()
        raw[g['name']] = {'menu': menu, 'answer': answer}
        built.append((pr_goal(g, answer, rel), menu))
        print(f'  {g["name"]:<24} {len(g["steps"]):>3} steps  {t2 - t0:5.1f}s'
              f'   decider {t1 - t0:4.1f}s  llm {t2 - t1:4.1f}s', flush=True)
    root, children = built[0][0], [b[0] for b in built[1:]]
    root['child'] = children
    dest = os.path.join(OUT, os.path.dirname(rel), '.build', os.path.basename(path)[:-5].lower() + '.pr')
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    json.dump(root, open(dest, 'w'), indent=1)
    # The raw menu + answer per goal, so a wrong .pr can be traced to the stage that made it wrong.
    json.dump(raw, open(dest + '.raw.json', 'w'), indent=1)
    return dest

if __name__ == '__main__':
    target = os.path.join(ROOT, sys.argv[1] if len(sys.argv) > 1 else 'os/system/builder')
    files = [target] if target.endswith('.goal') else sorted(
        f for f in glob.glob(f'{target}/**/*.goal', recursive=True) if '/.build/' not in f)
    for f in files:
        print(os.path.relpath(f, ROOT), flush=True)
        try: print('  ->', os.path.relpath(build(f), ROOT))
        except Exception as e: print('  FAILED:', type(e).__name__, e)
    print(f'\nshape deviations: {len(DEVIATIONS)}')
    for d in DEVIATIONS: print('  ', d)
