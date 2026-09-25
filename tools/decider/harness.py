"""Decider harness — measures the two-stage typesafe pipeline against the labels: the Tests/**/.pr
files, kept at tools/decider/labels/<same path under Tests/>.

stage 1  noul per (step, module)           -> module set per step      vs .pr modules
stage 2  choice per (step, module-in-set)  -> action per (step,module) vs .pr action

Raw answers are written to runs/<name>.jsonl so threshold sweeps and re-analysis need no API.
"""
import json, glob, os, sys, time, urllib.request, urllib.error, collections, concurrent.futures as cf

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
URL = 'https://api.typesafe.ai/v1/systemone'
MODEL = 'jev-latest'
KEY = os.environ.get('TYPESAFE_API_KEY') or open('/shared/hopkaup/secrets/typesafe.txt').read().strip()
OUT = os.path.join(os.path.dirname(__file__), 'runs')
os.makedirs(OUT, exist_ok=True)

# ---------------------------------------------------------------- catalogue
def first_line(p):
    for line in open(p, encoding='utf-8'):
        s = line.strip()
        if s: return s
    return ''

import re
def example_steps(p):
    """The `Step text: `...`` lines of an examples.md — the step shapes this action is for."""
    if not os.path.exists(p): return []
    return re.findall(r'Step text:\s*`([^`]+)`', open(p, encoding='utf-8').read())

# Descriptions on disk that describe ONE action instead of the module. Overridden here so the
# harness measures the decider, not the typo; each is a fix to propose for the .md.
DESCRIPTION_FIX = {
    'goal': 'Call another goal (`call X`), return from the current goal, and goal introspection',
}


def whole(p):
    return open(p, encoding='utf-8').read().strip() if os.path.exists(p) else None

def catalogue():
    """The WHOLE teaching set per module — description, notes, and per action description, notes,
    examples — exactly the markdown the builder's compile prompt is fed. Nothing summarised."""
    mods = {}
    for d in sorted(glob.glob(f'{ROOT}/os/system/modules/*/')):
        name = os.path.basename(d.rstrip('/'))
        if not os.path.exists(os.path.join(d, 'module.description.md')): continue
        acts = {}
        for a in sorted(glob.glob(d + '*.description.md')):
            an = os.path.basename(a)[:-len('.description.md')]
            if an == 'module': continue
            entry = {'description': whole(a)}
            for facet in ('notes', 'examples'):
                t = whole(os.path.join(d, f'{an}.{facet}.md'))
                if t: entry[facet] = t
            acts[an] = entry
        mod = {'description': DESCRIPTION_FIX.get(name) or whole(os.path.join(d, 'module.description.md'))}
        notes = whole(os.path.join(d, 'module.notes.md'))
        if notes: mod['notes'] = notes
        # The module's example steps, lifted from its actions' examples.md — the on-disk teaching,
        # shown at module level so stage 1 can recognise the shapes without seeing the action docs.
        ex = [t for an in acts for t in example_steps(os.path.join(d, f'{an}.examples.md'))]
        if ex: mod['example_steps'] = ex
        mod['actions'] = acts
        mods[name] = mod
    return mods

# ---------------------------------------------------------------- dataset
CHOSEN = [   # 5 goals, varied size and modules; one long one
    'tools/decider/labels/Modules/Http/DownloadSkip/.build/downloadskip.test.pr',        #  6 steps: http, file, condition, output
    'tools/decider/labels/BuilderSanity/.build/buildersanity.test.pr',                    #  7 steps: condition, goal, loop
    'tools/decider/labels/Modules/Variable/Scoping/.build/variablescoping.test.pr',       # 12 steps: goal, list, loop
    'tools/decider/labels/Condition/Compound/.build/conditioncompound.test.pr',           # 18 steps: if + call
    'tools/decider/labels/Modules/List/.build/listops.test.pr',                           # 34 steps: the long one
]

FRESH = [   # five goals NOT tuned on: modules with no example files, and Icelandic goal names
    'tools/decider/labels/Http/ConfigBaseUrl/.build/configbaseurl.test.pr',                          # http, condition, output
    'tools/decider/labels/App/Retry/.build/retry.test.pr',                                            # error, assert
    'tools/decider/labels/Modules/Signing/ContractMismatch/.build/signingcontractmismatch.test.pr',   # signing, identity
    'tools/decider/labels/Builder/.build/mockllmsmoke.test.pr',                                       # llm, mock
    'tools/decider/labels/Modules/Test/EdgeCase/.build/testdiscoverhandlesicelandicgoalnames.test.pr',# Icelandic names, test, list
]

BUILDER = [   # the builder itself, written in plang — the hardest goals we have
    'os/system/builder/BuildGoal/.build/start.pr',     # 17 steps: build, file, goal, list, loop, math, ui, variable
    'os/system/builder/.build/build.pr',                # 11 steps: build, channel, file, goal, loop, variable
    'os/system/builder/BuildGoal/.build/plan.pr',       #  9 steps: goal, ui, variable
    'os/system/builder/BuildGoal/.build/llmfixer.pr',   #  3 steps: goal, llm, variable
    'os/system/builder/BuildStep/.build/validate.pr',   #  3 steps: condition, goal, loop
]

WIDE = [   # ten more unseen goals, picked for module variety
    'tools/decider/labels/TestModule/TypedReturns/Stage1/.build/testrunnerpipelinestillworksaftertesterfilerename.test.pr',
    'tools/decider/labels/Modules/Signing/TimedOut/.build/signingtimedout.test.pr',
    'tools/decider/labels/Modules/Signing/Expired/.build/signingexpired.test.pr',
    'tools/decider/labels/Http/UploadFile/.build/uploadfile.test.pr',
    'tools/decider/labels/Modules/Test/Report/.build/testreportwritesjunitxml.test.pr',
    'tools/decider/labels/Event/Multiple/.build/eventmultiple.test.pr',
    'tools/decider/labels/Modules/Cache/DynamicKey/.build/cachedynamickey.test.pr',
    'tools/decider/labels/Modules/Event/Remove/.build/eventremove.test.pr',
    'tools/decider/labels/Modules/Signing/DotNavigation/.build/signingdotnavigation.test.pr',
    'os/system/error/.build/consoleerror.pr',
]

HOLDOUT = [   # never seen: picked at random from everything not yet used
    'tools/decider/labels/Signing/HeaderMismatch/.build/signingheadermismatch.test.pr',
    'tools/decider/labels/Modules/Test/Report/.build/testreportmaskssensitivevariables.test.pr',
    'tools/decider/labels/Modules/Signing/Roundtrip/.build/signingroundtrip.test.pr',
    'tools/decider/labels/Crypto/VerifyWrongHash/.build/verifywronghash.test.pr',
    'tools/decider/labels/CompareRedesign/Plane_MembershipNeverErrors/.build/plane_membershipnevererrors.test.pr',
    'tools/decider/labels/CompareRedesign/Cut1_CrossTypeAntisymmetry/.build/cut1.test.pr',
    'tools/decider/labels/Modules/Test/Report/.build/testreportrendersfailurewithvariables.test.pr',
    'tools/decider/labels/Identity/Unarchive/.build/identityunarchive.test.pr',
]

TWO = ['tools/decider/labels/App/CallStack/.build/throwitem.pr']

SETS = {'two': TWO, 'fresh': FRESH, 'builder': BUILDER, 'wide': WIDE, 'holdout': HOLDOUT}

def goals(limit=None, seed=0, chosen=True):
    out = []
    picks = SETS.get(os.environ.get('SET', ''), CHOSEN)
    files = [f'{ROOT}/{p}' for p in picks] if chosen else sorted(glob.glob(f'{ROOT}/tools/decider/labels/**/.build/*.pr', recursive=True))
    for f in files:
        try: d = json.load(open(f, encoding='utf-8'))
        except Exception: continue
        sts = d.get('step') or d.get('steps') or []
        steps = []
        def walk(a):   # an action, its modifiers, its child steps, and recovery actions riding as a parameter value
            if a.get('module'): yield (a['module'], a.get('action') or a.get('name'))
            for mod in a.get('modifier') or a.get('modifiers') or []: yield from walk(mod)
            for child in a.get('child') or []:
                for ca in child.get('action') or child.get('actions') or []: yield from walk(ca)
            for p in a.get('property') or a.get('parameter') or a.get('parameters') or []:
                v = p.get('value')
                if isinstance(v, list):
                    for ra in v:
                        if isinstance(ra, dict) and ra.get('module'): yield from walk(ra)
        for st in sts:
            acts = st.get('action') or st.get('actions') or []
            label = [x for a in acts for x in walk(a)]
            if not label or not st.get('text'): continue
            steps.append({'index': len(steps), 'text': st['text'], 'label': label,
                          'indent': st.get('indent') or 0, '_raw': acts})
        if steps:
            out.append({'file': os.path.relpath(f, ROOT), 'name': d.get('name', ''), 'steps': steps})
    if seed:
        import random; random.Random(seed).shuffle(out)
    return out[:limit] if limit else out

# ---------------------------------------------------------------- api
import threading
_local = threading.local()   # _local.dump = (folder, label) writes the raw request/response of every call — per thread

def ask(state, questions, retries=4):
    payload = {'state': state, 'model': MODEL, 'questions': questions}
    body = json.dumps(payload).encode()
    DUMP = getattr(_local, 'dump', None)
    if DUMP:
        folder, label = DUMP
        json.dump(payload, open(os.path.join(folder, f'{label}.request.json'), 'w'), indent=2, ensure_ascii=False)
    for attempt in range(retries):
        if attempt: time.sleep(2 ** attempt)
        req = urllib.request.Request(URL, data=body, method='POST', headers={
            'Authorization': f'Bearer {KEY}', 'Content-Type': 'application/json', 'User-Agent': 'plang decider harness'})
        t0 = time.time()
        try:
            with urllib.request.urlopen(req, timeout=180) as r:
                resp = json.loads(r.read())
            if DUMP:
                folder, label = DUMP
                json.dump(resp, open(os.path.join(folder, f'{label}.response.json'), 'w'), indent=2, ensure_ascii=False)
            return resp, time.time() - t0, len(body)
        except urllib.error.HTTPError as e:
            txt = e.read().decode(errors='replace')
            if e.code in (408, 429, 500, 502, 503, 504, 529): continue
            raise RuntimeError(f'{e.code}: {txt[:300]}')
    raise RuntimeError('retries exhausted')

# ---------------------------------------------------------------- stages
GUIDANCE = (
    'plang is natural-language code: each entry in `steps` is one step, written in any human language. '
    'A step is carried out by one or more modules; each module has actions. `modules.<name>` holds a '
    'module\'s full documentation: what it does, its actions, and example steps. A step uses a module '
    'if carrying out the step needs one of that module\'s actions ANYWHERE in the step — including a '
    'call that sits inside an if/foreach clause, and a trailing "write to %x%" clause (which stores a '
    'result and uses the variable module). Each module\'s `example_steps` are steps that use it.')

WINDOW = 15   # steps asked about per request; the state always carries the whole goal for context

TEXT_STATE = True   # v0.1's shape: plain text, steps numbered from 1, catalogue appended

# How plang is structured — taught once, in plain words, with no syntax. Intent only: the
# developer writes a step in any human language, so no wording is ever assumed.
STRUCTURE = '''How plang is structured:
- A goal is a list of steps. Each step is a sentence saying what to do, written in any human language. There is no syntax and no reserved words; only the intent matters.
- A step is carried out by one or more actions. Every action belongs to exactly one module.
- Within a step the actions form a chain: the result of one action can feed the next one.
- A step may keep its result for later use by naming a variable (a name between % signs). That is the variable module's job, in addition to whatever module did the work.
- A step may guard part of its work behind a condition (condition module), repeat part of it for each item of a collection (loop module), or attach error handling to it (error module). Each of those is an action of its own, and whatever runs INSIDE the guard, the loop or the error handler is also an action of its own — so calling a goal inside an if, a loop or an error handler uses the goal module as well.
- So one step often uses several modules: the one doing the main work; condition/loop/error for a surrounding clause; goal when a goal is called anywhere in it; variable when the result is kept.
- A module counts as used by a step whenever the step's sentence names work that module does, EVEN IF that work only runs in some cases — the body of a condition, of a loop, or of an error handler is named by the step and counts, regardless of whether it happens to run.
- A step that REGISTERS something to happen later — an event handler, a callback, a scheduled goal — does not do that work now. Naming a goal to be run later is not calling it; the step uses the module that does the registering.
- Steps are listed in order and may be indented. An indented step is the body of the step above it, and belongs to that step; each step is still asked about on its own, for what its own sentence does.'''

# Steps are numbered as the .pr numbers them, from 0: step N is index N everywhere.
def step_no(s): return s['index']

def state_for(goal, cat, modules=None):
    """Stage 1 sees MODULE-level docs only (what each module is for) — it decides modules.
    Stage 2 passes `modules`: only the chosen ones, with their full action docs — it decides actions."""
    if modules is None:
        shown = {m: {k: v[k] for k in ('description', 'notes', 'example_steps') if k in v} for m, v in cat.items()}
    else:
        shown = {m: cat[m] for m in sorted(modules)}
    if not TEXT_STATE:
        return {'guidance': GUIDANCE, 'goal': goal['name'],
                'steps': [{'index': s['index'], 'text': s['text']} for s in goal['steps']], 'modules': shown}

    lines = [STRUCTURE, '', f'This is a plang goal called {goal["name"]}. Its steps are numbered from 0.', '']
    # Indentation is structure: an indented step is the body of the step above it.
    for s in goal['steps']:
        lines.append(f'step {step_no(s)}: ' + '    ' * s.get('indent', 0) + s['text'])
    lines += ['', 'These are the plang modules you may choose from:']
    for m, v in shown.items():
        lines.append(f'- {m}: {v["description"]}')
        if v.get('notes'): lines.append(f'  notes: {v["notes"]}')
        for ex in v.get('example_steps', []): lines.append(f'  e.g. `{ex}`')
        if modules is not None:   # stage 2: the module's actions, in full
            for an, av in v['actions'].items():
                lines.append(f'  action {m}.{an}: {av["description"]}')
                if av.get('examples'): lines.append(f'    examples: {av["examples"]}')
    if modules is None:   # stage 1 also asks these actions by name: described once, here
        lines += ['', 'These actions are asked about by name:']
        for a in COMMON:
            m, an = a.split('.', 1)
            lines.append(f'- {a}: {cat[m]["actions"][an]["description"]}')
    return '\n'.join(lines)

def windows(steps):
    for i in range(0, len(steps), WINDOW):
        yield steps[i:i + WINDOW]

# The actions most steps use, asked by name in stage 1 beside the modules — counted, not guessed: the
# share of steps using each across tools/decider/labels/, the builder's own .pr files and the golden
# set (variable.set 42%, goal.call 8.7%, output.write 6.4%, error.handle 6.2%, condition.if 4.2%,
# file.read 2.3%). The assert.* actions rank high only because the labels are mostly tests.
COMMON = ['variable.set', 'goal.call', 'output.write', 'error.handle', 'condition.if', 'file.read']
NEAR_CERTAIN = 0.9   # a common action scored at or above this is picked; its module skips stage 2

# What a common-action question counts as the step's own. CLAUSE=1 adds "its own work, or anything it
# guards, loops or hands to an error handler"; measured on the golden goals it made output.write and
# error.handle fire where they don't belong (46/58 steps exact at 0.5 against 51/58 without), so the
# bare question is the default.
CLAUSE = (' — its own work, or anything it guards behind a condition, repeats in a loop, or hands to an '
          'error handler —') if os.environ.get('CLAUSE', '0') == '1' else ''

def stage1(goal, cat):
    """v0.1's shape (origin/main PLang/Building/StepBuilder.cs PrefetchModules) plus the common actions:
    per step ONE choice — which module does the main work — whose options are the bare module names
    (their descriptions and example steps are in the shared state, once), and a noul per common action.
    Every score is kept: probs[i] holds each module's probability from the choice and each common
    action's noul."""
    state = state_for(goal, cat)
    options = {m: None for m in cat}
    probs = collections.defaultdict(dict); secs = nbytes = nq = 0; usage = collections.Counter()
    for win in windows(goal['steps']):
        qs = {}
        for s in win:
            text = s['text'].strip()
            qs[f's{s["index"]}_@module'] = {'type': 'choice', 'criteria': options, 'instructions':
                f'Step {step_no(s)} of this goal is `{text}`. Which plang module does the main work of step {step_no(s)}?'}
            for a in COMMON:
                qs[f's{s["index"]}_{a}'] = {'type': 'noul', 'instructions':
                    f'Step {step_no(s)} is `{text}`. Does step {step_no(s)}{CLAUSE} use `{a}`?'}
        resp, t, b = ask(state, qs)
        secs += t; nbytes += b; nq += len(qs); usage.update(resp.get('usage', {}))
        for k, a in resp['answers'].items():
            i, m = k[1:].split('_', 1)
            if m == '@module': probs[int(i)].update(a.get('probabilities') or {a.get('choice'): a.get('confidence')})
            else: probs[int(i)][m] = a.get('noul')
    return probs, secs, nbytes, nq, dict(usage)

def main_module(probs_i, cat):
    """The module the step's choice named: the most probable one."""
    scored = {m: p for m, p in probs_i.items() if m in cat and p is not None}
    return max(scored, key=scored.get) if scored else None

RUNNER_UP = 0.2   # the choice's second module at or above this is asked in stage 2 as well

def picks(probs_i, cat, threshold=0.5):
    """One step's stage-1 answer as (common-action picks {action: score}, modules stage 2 must ask): the
    main module, and the runner-up when its probability is at least RUNNER_UP — a step whose work two
    modules share — each unless one of its common actions is near-certain, which is then its answer."""
    common = {a: probs_i.get(a) for a in COMMON if probs_i.get(a) is not None}
    settled = {a.split('.', 1)[0] for a, p in common.items() if p >= NEAR_CERTAIN}
    ranked = sorted(((m, p) for m, p in probs_i.items() if m in cat and p is not None), key=lambda mp: -mp[1])
    asked = ranked[:1] + [(m, p) for m, p in ranked[1:2] if p >= RUNNER_UP]
    return common, [m for m, _ in asked if m not in settled]

# The branches that can follow an if in the same step. Stage 2 is one choice per module, so it can
# never give two actions of the condition module; a picked condition.if asks these by name.
BRANCHES = ['condition.elseif', 'condition.else']

def stage2(goal, cat, chosen, conditions=()):
    """chosen: {step_index: [modules]} — one choice per (step, module) for its action. conditions: the
    steps with condition.if picked — each also asked noul for every BRANCHES action, answered as
    out[(i, 'condition.else')] = (None, noul)."""
    used = {m for ms in chosen.values() for m in ms} | ({'condition'} if conditions else set())
    state = state_for(goal, cat, modules=used)
    out = {}; secs = nbytes = nq = 0; usage = collections.Counter()
    for win in windows(goal['steps']):
        qs = {}
        for s in win:
            if s['index'] in conditions:
                for a in BRANCHES:
                    qs[f's{s["index"]}_{a}'] = {'type': 'noul', 'instructions':
                        f'Step {step_no(s)} is `{s["text"].strip()}`. It tests a condition. Does step {step_no(s)} also have `{a}` — '
                        f'{cat["condition"]["actions"][a.split(".", 1)[1]]["description"]}?'}
            for m in chosen.get(s['index'], []):
                acts = cat[m]['actions']
                if len(acts) == 1:
                    out[(s['index'], m)] = (next(iter(acts)), 1.0); continue
                qs[f's{s["index"]}_{m}'] = {
                    'type': 'choice',
                    'instructions': f'Step {step_no(s)} of this goal is `{s["text"].strip()}`. It uses the plang module `{m}`. Which action of `{m}` does step {step_no(s)} call?',
                    'criteria': {an: av['description'] for an, av in acts.items()}}
        if not qs: continue
        resp, t, b = ask(state, qs)
        secs += t; nbytes += b; nq += len(qs); usage.update(resp.get('usage', {}))
        for k, a in resp['answers'].items():
            i, m = k[1:].split('_', 1)
            out[(int(i), m)] = (None, a.get('noul')) if m in BRANCHES else (a.get('choice'), a.get('confidence'))
    return out, secs, nbytes, nq, dict(usage)

# ---------------------------------------------------------------- run
def run(name, limit, seed, threshold=0.5, workers=4):
    cat = catalogue()
    data = goals(limit, seed)
    path = os.path.join(OUT, f'{name}.jsonl')
    print(f'{len(data)} goals, {sum(len(g["steps"]) for g in data)} steps, {len(cat)} modules -> {path}', flush=True)

    def one(goal):
        rec = {'goal': goal['name'], 'file': goal['file'], 'steps': goal['steps']}
        try:
            probs, s1, b1, q1, u1 = stage1(goal, cat)
            chosen = {i: picks(probs[i], cat, threshold)[1] for i in probs}
            acts, s2, b2, q2, u2 = stage2(goal, cat, chosen)
            rec.update(stage1={'probs': {str(i): probs[i] for i in probs}, 'secs': s1, 'bytes': b1, 'questions': q1, 'usage': u1},
                       stage2={'choice': {f'{i}|{m}': v for (i, m), v in acts.items()}, 'secs': s2, 'bytes': b2, 'questions': q2, 'usage': u2})
        except Exception as e:
            rec['error'] = str(e)
        return rec

    with open(path, 'w') as f, cf.ThreadPoolExecutor(workers) as ex:
        for n, rec in enumerate(ex.map(one, data), 1):
            f.write(json.dumps(rec) + '\n'); f.flush()
            if n % 10 == 0 or n == len(data): print(f'  {n}/{len(data)}', flush=True)
    return path

if __name__ == '__main__':
    name = sys.argv[1] if len(sys.argv) > 1 else 'sample'
    limit = int(sys.argv[2]) if len(sys.argv) > 2 else 20
    seed = int(sys.argv[3]) if len(sys.argv) > 3 else 7
    run(name, limit, seed)
