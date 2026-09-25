"""Prompt C — the decider's picks pre-filled in formal, the answer in formal, and the double check.

    system  os/system/builder/llm/PropertiesC.llm (no schema: the answer is formal text)
    user    the goal as written; under each step the decider's picks ≥ 0.5 with their scores and a formal
            line with the certain ones (≥ 0.9) pre-filled — `?` for a value to fill, known values filled
            (`write to %x%` → variable.set(Name=%x%, Value=%!data%)); then Types; then each listed action
            once (signature, description line, notes), and goal.call's definition when a listed action
            holds actions (an action-typed property, error.handle's Recovery)

The check — the LLM and the decider must agree:
    refused   a pick ≥ 0.9 missing from the step, or an action the decider did not list for it
              (goal.call held as a value or a recovery is allowed: it is not the step's own action)
    warning   the step was built from a possible pick (0.5–0.9), or a disagreement was settled on retry
"""
import collections, os, re
import build_pr as b
import formal as f

ROOT = b.ROOT
SYSTEM_C = open(f'{ROOT}/os/system/builder/llm/PropertiesC.llm', encoding='utf-8').read()
CERTAIN, POSSIBLE = 0.9, 0.5
WRITE_TO = re.compile(r'write to\s+(%[^%\s]+%)', re.I)

def listed(picks_i, text=''):
    """The step's picks shown to the LLM: ≥ 0.5, most certain first. A step that says `write to %x%`
    has variable.set whatever the decider scored it: that is a known value, not a guess. An unsure step
    adds the top 3 of the decider's popular-action choice, whatever their probability."""
    out = {a: p for a, p in picks_i.items() if not a.startswith('@') and p is not None and p >= POSSIBLE}
    if WRITE_TO.search(text) and 'variable.set' not in out: out['variable.set'] = picks_i.get('variable.set') or 0.0
    for a, p in (picks_i.get('@popular') or {}).items():
        if a not in out: out[a] = p or 0.0
    return sorted(out.items(), key=lambda ap: -ap[1])

def popular_only(picks_i):
    """The actions an unsure step lists only because the popular-action choice ranked them."""
    return {a for a in (picks_i.get('@popular') or {})
            if not ((picks_i.get(a) or 0) >= POSSIBLE)}

def is_unsure(picks_i, text=''):
    """The decider was unsure of the step: it asked the popular-action choice (harness.unsure)."""
    return bool(picks_i.get('@popular'))

ON_ERROR_CALL = re.compile(r'on error[^,;]*?\bcall\b', re.I)
ARGUMENTS = re.compile(r'\bcall\b[^,]*?\s[A-Za-z_]\w*\s*=', re.I)   # `call X name=value`: the step passes arguments

def holds_actions(action):
    """Does this action take actions as a value — an action-typed property, or a modifier's Recovery?"""
    module, name = action.split('.', 1)
    props, _ = b.declared(module, name)
    return f.takes_recovery(module, name) or any(p['type'] == 'action' for p in props.values())

def prefill(action, text):
    """One pick as the formal line pre-fills it: its required properties as `?`, what the step already
    says filled — `write to %x%` → variable.set(Name=%x%, Value=%!data%). A modifier wraps `{ ? }`."""
    module, name = action.split('.', 1)
    props, modifier = b.declared(module, name)
    if action == 'variable.set' and (m := WRITE_TO.search(text)):
        return f'variable.set(Name={m.group(1)}, Value=%!data%)'
    required = [f'{n}=?' for n, p in props.items() if not p['nullable'] and p['default'] is None]
    if action == 'goal.call' and ARGUMENTS.search(text): required.append('Parameter={?}')
    s = f'{action}(' + ', '.join(required) + ')'
    return s + ' { ? }' if modifier else s

def user_message_c(goal, picks):
    """picks: {step index: {action: score}} — every score the decider gave."""
    lines = []
    for s in goal['steps']:
        lines.append((s, f'[{s["index"]}] {"    " * s.get("indent", 0)}- {s["text"]}'))
    out = 'Goal, as written:\n  ' + goal['name']
    shown = []
    for s, line in lines:
        pad = ' ' * (len(f'[{s["index"]}] ') + 4 * s.get('indent', 0))
        for c in (s.get('comment') or '').split('\n') if s.get('comment') else []:
            out += f'\n  {pad}/ {c}'
        out += f'\n  {line}'
        step_picks = listed(picks.get(s['index'], {}), s['text'])
        known = WRITE_TO.search(s['text'])
        pop = popular_only(picks.get(s['index'], {}))
        decider = ', '.join(f'{a} {p:.2f}' + ('' if p >= CERTAIN else ' (write to)' if a == 'variable.set' and known
                                              else ' (possible, popular)' if a in pop else ' (possible)')
                            for a, p in step_picks)
        out += f'\n  {pad}  decider: {decider or "(nothing it is sure of)"}'
        # the certain picks pre-filled; a known value (write to) pre-fills its variable.set, last
        certain = [a for a, p in step_picks if p >= CERTAIN and not (a == 'variable.set' and known)]
        filled = [prefill(a, s['text']) for a in certain if not b.declared(*a.split('.', 1))[1]]
        # a certain modifier is shown wrapping the step's first action — `{ }` is what it wraps, not what it runs
        for m in [a for a in certain if b.declared(*a.split('.', 1))[1]]:
            head = prefill(m, s['text'])[:-len(' { ? }')]
            if f.takes_recovery(*m.split('.', 1)):
                # `on error call X` names what the recovery runs: a goal.call — a known value, like write to
                recovery = 'goal.call(Name=?)' if ON_ERROR_CALL.search(s['text']) else '?'
                head = head[:-1] + ('' if head[:-1].endswith('(') else ', ') + f'Recovery=[{recovery}])'
            filled = [f'{head} {{ {filled[0] if filled else "?"} }}'] + filled[1:]
        if known: filled.append(prefill('variable.set', s['text']))
        if filled: out += f'\n  {pad}  formal:  ' + '; '.join(filled)
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
    for a in shown:
        module, name = a.split('.', 1)
        out += f'\n\n## {b.signature(a)}'
        if d := b.doc(module, name, 'description'): out += '\n' + d.split('\n')[0]
        if n := b.doc(module, name, 'notes'): out += '\n' + n.replace('## ', '### ')
    return out + '\n'

# ---------------------------------------------------------------- the check
def own_actions(rows):
    """The step's own actions: top level, a condition's body, the modifiers — not what a property holds
    (channel.set's Goal) nor a modifier's Recovery."""
    out = []
    for a in rows:
        out.append(f'{a["module"]}.{a["name"]}')
        for c in a.get('child') or []: out += own_actions(c.get('action') or [])
        for m in a.get('modifier') or []: out.append(f'{m["module"]}.{m["name"]}')
    return out

def held_actions(rows):
    """Actions held as values: a property's action, a Recovery's actions (and what those hold)."""
    out = []
    def walk(a):
        for r in a.get('property') or []:
            v = r['value']
            for x in (v if isinstance(v, list) else [v]):
                if isinstance(x, dict) and x.get('module'): out.append(f'{x["module"]}.{x["name"]}'); walk(x)
        for m in a.get('modifier') or []:
            for x in m.get('recovery') or []: out.append(f'{x["module"]}.{x["name"]}'); walk(x)
            walk(m)
        for c in a.get('child') or []:
            for x in c.get('action') or []: walk(x)
    for a in rows: walk(a)
    return out

def disagreements(i, rows, picks_i, text=''):
    """What the LLM and the decider disagree on in step i: (refusals, unsure)."""
    used = own_actions(rows)
    shown = dict(listed(picks_i, text))
    refused = [f'step {i} leaves out {a}, which the decider is certain of ({p:.2f})'
               for a, p in shown.items() if p >= CERTAIN and a not in used]
    refused += [f'step {i} uses {a}, which the decider did not list for it'
                for a in dict.fromkeys(used) if a not in shown]
    # an action listed only through the popular-action choice builds with a warning of its own
    popular = [a for a in dict.fromkeys(used) if a in popular_only(picks_i)]
    refused += [f'step {i} holds {a}, which is not listed; only goal.call may be held without being listed'
                for a in dict.fromkeys(held_actions(rows)) if a != 'goal.call' and a not in shown]
    # a recovery that runs the very action it wraps (the same action, the same values) is not a recovery
    def same(x, y):
        return (x['module'], x['name']) == (y['module'], y['name']) and \
               [(r['name'], r['value']) for r in x.get('property') or []] == [(r['name'], r['value']) for r in y.get('property') or []]
    for a in rows:
        for m in a.get('modifier') or []:
            if any(same(x, a) for x in m.get('recovery') or []):
                refused.append(f'step {i}: the Recovery of {m["module"]}.{m["name"]} runs {a["module"]}.{a["name"]}, the action it wraps — '
                               'Recovery holds what the step runs on error')
    # unsure = built from a possible pick; a pick the known-value rule placed (write to → variable.set)
    # is not a guess, so it carries no warning
    known = {'variable.set'} if WRITE_TO.search(text) else set()
    warnings = [f'step {i} uses {a}, which the decider was not sure of ({shown[a]:.2f})'
                for a in dict.fromkeys(used) if a in shown and shown[a] < CERTAIN and a not in known and a not in popular]
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
                if isinstance(x, dict) and x.get('module'): drop_nulls([x])
        for c in a.get('child') or []: drop_nulls(c.get('action') or [])
        drop_nulls(a.get('modifier') or [])
        for m in a.get('modifier') or []: drop_nulls(m.get('recovery') or [])

def uncovered(text, rows):
    """The %variables% the step's text writes that its answer doesn't hold anywhere — a value, a Name,
    inside a quoted text. %x% is plang's own marker, so this reads no human language."""
    written = f.write_actions(rows, types=False)
    return [v for v in dict.fromkeys(f.VARIABLE.findall(text)) if v not in written]

# A quoted literal in a step's text — "…" or '…' (a single quote only when not inside a word, so
# `don't` is not one). Quotes are the programmer's literal marker, not a human language.
LITERAL = re.compile(r'"([^"]*)"|(?<!\w)\'([^\']*)\'(?!\w)')

def values_of(rows):
    """Every text the answer holds — each value, each dict key and argument name — however deep."""
    out = []
    def walk(v):
        if isinstance(v, dict):
            for k, x in v.items():
                if k not in ('module', 'name', 'type', 'property', 'modifier', 'child', 'recovery', 'action', 'frozen'): out.append(str(k))
                walk(x)
        elif isinstance(v, list):
            for x in v: walk(x)
        elif isinstance(v, str): out.append(v)
    for a in rows:
        for r in a.get('property') or []: walk(r['value']); out.append(r['name'])
        for c in a.get('child') or []: out += values_of(c.get('action') or [])
        out += values_of(a.get('modifier') or [])
        for m in a.get('modifier') or []: out += values_of(m.get('recovery') or [])
    return out

def uncovered_literals(text, rows):
    """The quoted literals of the step's text that no value of its answer holds."""
    held = values_of(rows)
    lits = [m.group(1) if m.group(1) is not None else m.group(2) for m in LITERAL.finditer(text)]
    return [l for l in dict.fromkeys(lits) if l and not any(l in v for v in held)]

WHOLE = ('has no entry', 'is extra', 'is labelled', 'has no index', 'is not an object')

def check(goal, picks, parsed):
    """(whole-answer refusals, {step: refusals}, warnings) of a parsed formal answer {i: rows}.
    Whole: the answer doesn't line up with the goal (a step missing, extra, renumbered). Per step:
    no actions, the chain rule, a body over indented steps that isn't a copy (fold), the agreement
    with the decider, a %variable% of the step's text missing from its answer. Nulls on optional
    properties are dropped first."""
    for rows in parsed.values(): drop_nulls(rows)
    per_step = fold(goal, parsed)
    steps = goal['steps']
    whole = [f'step {i} ("{steps[i]["text"]}") has no entry' for i in range(len(steps)) if i not in parsed]
    whole += [f'entry {i} is extra: the goal has {len(steps)} steps' for i in parsed if i >= len(steps)]
    warnings = []
    for i, rows in parsed.items():
        if i >= len(steps): continue
        problems = per_step.setdefault(i, [])
        if not rows: problems.append(f'step {i} ("{steps[i]["text"]}") has no actions')
        elif broken := b.chain(rows, bool(b.body_of(steps, i))): problems.append(f'step {i} ("{steps[i]["text"]}") — {broken}')
        r, w = disagreements(i, rows, picks.get(i, {}), steps[i]['text'])
        problems += r; warnings += w
        problems += [f'step {i}: {v} is in the step but not in your answer' for v in uncovered(steps[i]['text'], rows)]
        problems += [f'step {i}: "{l}" is in the step but not in your answer' for l in uncovered_literals(steps[i]['text'], rows)]
    return whole, {i: p for i, p in per_step.items() if p}, warnings
