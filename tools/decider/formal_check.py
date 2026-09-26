"""Formal, checked on the golden set — no LLM calls.

1. Round trip: each expected answer (child_golden.json) as .pr rows → formal.write → formal.parse →
   equal to the rows it started from.
2. A .goal step written in formal is recognised (formal.is_formal) and parsed directly, and gives the
   rows the LLM path gives (build_pr.pr_action over the same answer as JSON); no natural step text is
   taken for formal.
3. The hand-written prompt-C answer (/shared/coder/2.0/checkout/expected.txt) parses to the golden rows.
4. Parse errors name the line and column.

    python3 formal_check.py
"""
import json, os, sys, copy
import formal as f
import build_pr as b

HERE = os.path.dirname(os.path.abspath(__file__))
GOLDEN = json.load(open(os.path.join(HERE, 'child_golden.json'), encoding='utf-8'))['cases']
MOCK = '/shared/coder/2.0/checkout/expected.txt'

def rows_of(a):
    """A golden expected action ({module, name, property: {Name: value}, child, modifier, recovery}) as
    .pr rows, each typed the way the parser types it: the declared type, or the literal's own in an
    open slot."""
    declared = f.properties(a['module'], a['name'])
    props = []
    for name, v in (a.get('property') or {}).items():
        if isinstance(v, dict) and '$oneOf' in v: v = v['$oneOf'][0]
        spec = declared.get(name, {'type': 'item'})
        if f.is_action(v): v = rows_of(v)
        elif spec['type'].startswith('list') and isinstance(v, list) and v and isinstance(v[0], dict) and 'name' in v[0]:
            v = [{'name': r['name'], 'type': f.typed('item', r['value']), 'value': r['value']} for r in v]
        props.append({'name': name, 'type': f.typed(spec['type'], v), 'value': v})
    out = {'module': a['module'], 'name': a['name'], 'property': props,
           'modifier': [rows_of(m) for m in a.get('modifier') or []]}
    if a.get('recovery'): out['recovery'] = [rows_of(r) for r in a['recovery']]
    if a.get('child'): out['child'] = [{'text': c['text'], 'action': [rows_of(x) for x in c['action']]} for c in a['child']]
    return out

def without_child_text(actions):
    """The rows with each child's text set aside — formal has no place for the body's words."""
    out = copy.deepcopy(actions)
    def strip(a):
        for c in a.get('child') or []:
            c.pop('text', None)
            for x in c['action']: strip(x)
        for m in a.get('modifier') or []: strip(m)
        for r in a.get('recovery') or []: strip(r)
        for p in a.get('property') or []:
            if f.is_action(p['value']): strip(p['value'])
    for a in out: strip(a)
    return out

def diff(x, y, at=''):
    """The first place two row trees differ, or None."""
    if type(x) != type(y) and not (isinstance(x, (int, float)) and isinstance(y, (int, float))):
        return f'{at}: {x!r} vs {y!r}'
    if isinstance(x, dict):
        for k in sorted(set(x) | set(y)):
            if k not in x or k not in y: return f'{at}.{k}: only on one side ({x.get(k, y.get(k))!r})'
            d = diff(x[k], y[k], f'{at}.{k}')
            if d: return d
        return None
    if isinstance(x, list):
        if len(x) != len(y): return f'{at}: {len(x)} items vs {len(y)}'
        for i, (p, q) in enumerate(zip(x, y)):
            d = diff(p, q, f'{at}[{i}]')
            if d: return d
        return None
    return None if x == y else f'{at}: {x!r} vs {y!r}'

def main():
    failures = []
    child_texts = []
    total = 0
    answers = {}
    # 1. round trip, and 2. the .goal formal step against the LLM path
    for c in GOLDEN:
        answers[c['id']] = {}
        for k, expected in sorted(c['expect'].items(), key=lambda kv: int(kv[0])):
            total += 1
            rows = [rows_of(a) for a in expected]
            # 1. both forms round-trip: the writer's (types written) and the untyped one the LLM
            #    answers and a programmer may write — the parser adds the types.
            for form, text in (('typed', f.write(rows)), ('untyped', f.write(rows, types=False))):
                if form == 'typed': answers[c['id']][int(k)] = text
                try:
                    back = f.parse(text)
                except f.FormalError as e:
                    failures.append(f'{c["id"]}[{k}] {form} does not parse back: {e}\n      {text}'); continue
                if d := diff(without_child_text(rows), without_child_text(back)):
                    failures.append(f'{c["id"]}[{k}] {form} round trip differs at {d}\n      {text}')
                if form == 'typed':
                    for a, a2 in zip(rows, back):
                        for ch, ch2 in zip(a.get('child') or [], a2.get('child') or []):
                            child_texts.append((f'{c["id"]}[{k}]', ch['text'], ch2['text']))
            # 2. the step as a .goal line in formal (untyped, as a programmer writes it), against the
            #    LLM path over the same answer as JSON
            goal_line = f.write(rows, types=False)
            if not f.is_formal(goal_line):
                failures.append(f'{c["id"]}[{k}] written in formal is not recognised as formal')
            llm = [b.pr_action(json.loads(json.dumps(a))) for a in rows]
            if d := diff(without_child_text(llm), without_child_text(f.parse(goal_line))):
                failures.append(f'{c["id"]}[{k}] formal step vs LLM path differ at {d}')
        # no natural step is taken for formal
        for i, s in enumerate(c['steps']):
            if f.is_formal(s['text']):
                failures.append(f'{c["id"]}[{i}] natural step text read as formal: {s["text"]}')

    # 3. the hand-written prompt-C answer
    mock = None
    if os.path.exists(MOCK):
        text = open(MOCK, encoding='utf-8').read()
        checkout = next(c for c in GOLDEN if c['id'] == 'checkout')
        try:
            parsed = f.parse_answer(text)
            mock = []
            for k, expected in checkout['expect'].items():
                d = diff(without_child_text([rows_of(a) for a in expected]), without_child_text(parsed.get(int(k), [])))
                mock.append((k, d))
        except f.FormalError as e:
            mock = [('parse', str(e))]

    # 3b. the notation's own cases, not in the golden: a frozen default, two modifiers after their action
    #     (the first written innermost; outermost first in the action's modifier list), an explicit type
    #     in an open slot
    own = {}
    for text in ['goal.return(Depth: number ?= 1)',
                 'goal.call(Name="X"); on.error(Key="B", Recovery=[goal.call(Name="RB")]); on.error(Key="A", Recovery=[goal.call(Name="RA")])',
                 'variable.set(Name=%d%, Value: date = "2026-01-01")']:
        try:
            rows = f.parse(text)
            again = f.parse(f.write(rows))
            own[text] = ('same' if not diff(rows, again) else 'DIFFERS: ' + diff(rows, again)), f.write(rows), rows
        except f.FormalError as e:
            own[text] = ('ERROR ' + str(e), '', None)

    # 4. errors name the line and column
    errors = {}
    for bad in ['file.read(Path="x"); variable.set(Name=%y% Value=%!data%)',
                'file.read(Path="x")\n    variable.set(Name=%y%, Value=%!data%)',
                'file.read(Pth="x")',
                'file.read(Path: text = "x")',
                'condition.if(Left=%n%, Operator="less", Right=5)',
                'variable.set(Name="y", Value=1)',
                'goal.call(Name="X")\n    on.error() { goal.call(Name="Y") }',
                'on.error(Recovery=[goal.call(Name="Y")])',
                'on.error() { goal.call(Name="X"); goal.call(Name="Y") }',
                'on.error(Recovery="Y") { goal.call(Name="X") }',
                'goal.call(Name="X") { output.write(Data="y") }',
                'file.read(Path="x"',
                'nope.nothing()']:
        try:
            f.parse(bad); errors[bad] = 'PARSED (should fail)'
        except f.FormalError as e:
            errors[bad] = str(e)

    # 5. A refused step reports every problem at once — checkout[4]'s first answer in round 9 run 2 (an
    # inverted if with an empty `{ }`, then a condition.else the step never had): one refusal, both lines.
    import c_eval as ce, child_eval as e
    run = os.path.join(HERE, 'runs', 'c_eval_round9_20260925_231637')
    picks = {int(k): v for k, v in json.load(open(os.path.join(run, 'decider.json')))['picks']['checkout'].items()}
    case = next(c for c in e.GOLDEN if c['id'] == 'checkout')
    parsed, errs, whole = f.parse_steps(open(os.path.join(run, 'C', 'gpt-5.4-nano', 'checkout', '1.answer.txt')).read())
    refusal = ce.judge_c(case, picks, parsed, errs, whole)[2].get(4, [])
    expected = ["the step has nothing indented below it, so what the step does when the condition holds goes inside the if's `{ }`",
                "condition.else isn't one of step 4's actions (condition.if, goal.call)"]
    if errs or not all(any(x in r for r in refusal) for x in expected):
        failures.append(f'checkout[4]: one refusal with every problem expected, got parse errors {errs} and {refusal}')

    # 6. The popular-action choice lists only options at or above the choice floor (0.2): start[0] in
    # round 10 (goal.call 0.79, popular output.write 0.01) renders without output.write.
    run10 = os.path.join(HERE, 'runs', 'c_eval_round10_20260925_234638')
    picks10 = {int(k): v for k, v in json.load(open(os.path.join(run10, 'decider.json')))['picks']['start'].items()}
    _, user10 = ce.request('C', next(c for c in e.GOLDEN if c['id'] == 'start'), picks10)
    line0 = next(l for l in user10.split('\n') if l.strip().startswith('[0]'))
    if 'output.write' in line0 or 'goal.call' not in line0:
        failures.append(f'start[0]: a popular option below the floor is listed: {line0.strip()}')

    print(f'round trip + formal step vs LLM path: {total} steps, {len(failures)} failures')
    for x in failures: print('  ', x)
    print(f'\nchild text: {len(child_texts)} bodies; formal keeps the body\'s actions, not its words:')
    for at, was, now in child_texts[:4]: print(f'   {at}: {was!r} -> {now!r}')
    if mock is not None:
        bad = [(k, d) for k, d in mock if d]
        print(f'\nmock answer {MOCK}: {len(mock) - len(bad)}/{len(mock)} steps equal the golden rows')
        for k, d in bad: print(f'   step {k}: {d}')
    print('\nthe notation\'s own cases:')
    for text, (verdict, written, rows) in own.items():
        print(f'   {text}\n      -> {verdict}; written back: {written!r}')
        if rows and 'on.error' in text:
            print(f'      wrapped: {rows[0]["module"]}.{rows[0]["name"]}, modifier keys in order: '
                  + ', '.join(next(r["value"] for r in m["property"] if r["name"] == "Key") for m in rows[0]["modifier"]))
        if rows and 'frozen' in json.dumps(rows): print('      frozen:', [r for r in rows[0]['property'] if r.get('frozen')])
    print('\nparse errors:')
    for t, e in errors.items(): print(f'   {t!r}\n      -> {e}')
    out = os.path.join(HERE, 'formal_golden.txt')
    with open(out, 'w', encoding='utf-8') as fh:
        for cid, steps in answers.items():
            fh.write(f'# {cid}\n' + f.write_answer({i: f.parse(t) for i, t in steps.items()}) + '\n\n')
    print('\nwrote', out)
    return 1 if failures else 0

if __name__ == '__main__':
    sys.exit(main())
