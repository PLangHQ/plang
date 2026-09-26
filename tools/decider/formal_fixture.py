"""The C# formal tests' fixture, written from the python reference (formal.py) — one entry per golden step:

    pr        the step's actions as .pr JSON rows, typed (what the C# action reader reads)
    formal    the typed formal the writer must produce (formal_golden.txt's line, without its [i])
    untyped   the same step as the LLM and a programmer write it (no types)

    python3 formal_fixture.py      → PLang.Tests/Wire/App/Serialization/formal_golden.json
"""
import json, os
import child_eval as e
import formal as f
import formal_check as fc

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', 'PLang.Tests', 'Wire', 'App', 'Serialization', 'formal_golden.json')

def pr_shape(v):
    """The rows as the .pr holds them: a step's actions are its `code` (python's rows keep `action` —
    build_pr.py moves to the .pr shape in 4d)."""
    if isinstance(v, list): return [pr_shape(x) for x in v]
    if not isinstance(v, dict): return v
    out = {k: pr_shape(x) for k, x in v.items()}
    if 'text' in out and 'action' in out and 'module' not in out: out['code'] = out.pop('action')
    return out

entries = []
for case in e.GOLDEN:
    for k, acts in sorted(case['expect'].items(), key=lambda kv: int(kv[0])):
        rows = [fc.rows_of(a) for a in acts]
        entries.append({'goal': case['id'], 'index': int(k), 'text': case['steps'][int(k)]['text'],
                        'pr': pr_shape(rows), 'formal': f.write(rows), 'untyped': f.write(rows, types=False)})
json.dump(entries, open(OUT, 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
print(len(entries), 'steps ->', os.path.relpath(OUT, os.path.join(HERE, '..', '..')))

# Every error the parser answers, with python's message — the C# parser's twins must say the same.
BAD = [
    'file.read(Path="x"); variable.set(Name=%y% Value=%!data%)',
    'file.read(Path="x")\n    variable.set(Name=%y%, Value=%!data%)',
    'file.read(Pth="x")',
    'file.read(Path: text = "x")',
    'condition.if(Left=%n%, Operator="less", Right=5)',
    'condition.if(Left=%n%, Operator=less, Right=5)',
    'condition.if(Left=%n%, Operator=="<", Right=5)',
    'variable.set(Name=5, Value=1)',
    'goal.call(Name="X")\n    on.error() { goal.call(Name="Y") }',
    'on.error(Recovery=[goal.call(Name="Y")])',
    'on.error() { goal.call(Name="X"); goal.call(Name="Y") }',
    'on.error(Recovery="Y") { goal.call(Name="X") }',
    'goal.call(Name="X") { output.write(Data="y") }',
    'condition.if(Left=%a%, Operator=isempty) { output.write(Data="x") }; condition.else() { }',
    'file.read(Path="x"',
    'nope.nothing()',
    'goal.call(Name="X", Parameter=[kind="a"])',
    'output.write(Data=hello)',
    'file.read(Path=%?)',
    'file.read(Path="x") · output.write(Data="y")',
    'on.error(Recovery=[goal.call(…)]) { goal.call(Name="X") }',
    'variable.set(Name=%d%, Value={name: text = "a"})',
    'condition.if(Left=%n%, Operator=<>, Right=5)',
    'on.error(Recovery=[goal.call(Name="Fix")]); file.read(Path="x")',
    'file.read(Path="x"); on.error(Recovery=[goal.call(Name="Fix")]) { goal.call(Name="Y") }',
    '',
]

# A choice's symbol option written bare reads as the option — the C# reader must read each the same.
BARE = [f'condition.if(Left=%n%, Operator={op}, Right=5) {{ goal.return() }}' for op in ('==', '!=', '>', '<', '>=', '<=')]
bare = [{'input': text, 'formal': f.write(f.parse(text))} for text in BARE]
json.dump(bare, open(OUT.replace('formal_golden.json', 'formal_bare.json'), 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
print(len(bare), 'bare choice cases:', '; '.join(b['formal'].split('Operator: ')[1].split(',')[0] for b in bare))
errors = []
for bad in BAD:
    try:
        f.parse(bad); errors.append({'input': bad, 'message': None})
    except f.FormalError as ex:
        errors.append({'input': bad, 'message': str(ex)})
json.dump(errors, open(OUT.replace('formal_golden.json', 'formal_errors.json'), 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
print(len(errors), 'error cases, python messages written')
