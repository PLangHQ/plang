"""The C# pick.line twin fixture — each case a step's pre-filled formal line built by the same moves (prompt_c.Line),
and what python writes for it. PLang.Tests' LineTwinTests replays the moves on goal/step/pick/line and compares.

    python3 line_fixture.py   → PLang.Tests/Wire/App/Decider/line_golden.json
"""
import json, os
import prompt_c as c

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', 'PLang.Tests', 'Wire', 'App', 'Decider', 'line_golden.json')

# (name, nests, moves): a move is ['add', call, opens] | ['insert', call] | ['append', call]
CASES = [
    ('a lone if holds the step\'s other action in its body', True,
     [['add', 'condition.if(Left=?)', True], ['add', 'goal.return()', False]]),
    ('a lone if with nothing else stays bare', True,
     [['add', 'condition.if(Left=?)', True]]),
    ('a chain stays flat', False,
     [['add', 'condition.if(Left=?)', True], ['add', 'condition.else()', False], ['add', 'goal.call(Name=?)', False]]),
    ('a step with a body below stays flat', False,
     [['add', 'condition.if(Left=?)', True], ['add', 'goal.return()', False]]),
    ('a clause follows its action inside the body; write to stays after the if', True,
     [['add', 'condition.if(Left=?)', True], ['add', 'goal.call(Name=?)', False],
      ['insert', 'on.error(Recovery=[goal.call(Name=?)])'], ['append', 'variable.set(Name=%x%, Value=%!data%)']]),
    ('a clause with no action yet leaves ? for it, and the action takes it', False,
     [['insert', 'on.error(Recovery=?)'], ['add', 'file.read(Path=?)', False]]),
    ('two clauses: the later one right after the action', False,
     [['add', 'goal.call(Name=?)', False], ['insert', 'on.error(Recovery=?)'], ['insert', 'on.timeout(After=?)']]),
    ('a loop leads, whatever its score put before it', False,
     [['add', 'goal.call(Name=?)', False], ['lead', 'loop.foreach(Collection=?)']]),
    ('set %x% = %x% + 1: the keep follows the action that produces its value, keeping %!data%', False,
     [['keep', 'variable.set(Name=?, Value=?)', 'variable.set(Name=?, Value=%!data%)'], ['add', 'math.add(A=?, B=?)', False, True]]),
    ('set %x% = 5, write out %x%: a sink produces nothing, so the keep stays first', False,
     [['keep', 'variable.set(Name=?, Value=?)', 'variable.set(Name=?, Value=%!data%)'], ['add', 'output.write(Data=?)', False, False]]),
    ('a keep follows the producers, and a write-to stays last', False,
     [['keep', 'variable.set(Name=?, Value=?)', 'variable.set(Name=?, Value=%!data%)'], ['add', 'math.add(A=?, B=?)', False, True],
      ['append', 'variable.set(Name=%y%, Value=%!data%)']]),
]

out = []
for name, nests, moves in CASES:
    line = c.Line(nests)
    for m in moves:
        if m[0] == 'add': line.add(m[1], m[2], m[3] if len(m) > 3 else False)
        elif m[0] == 'insert': line.insert(m[1])
        elif m[0] == 'lead': line.lead(m[1])
        elif m[0] == 'keep': line.keep(m[1], m[2])
        else: line.append(m[1])
    out.append({'name': name, 'nests': nests, 'moves': moves, 'written': line.written()})
json.dump(out, open(OUT, 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
print(f'{len(out)} cases -> {os.path.relpath(OUT, HERE)}')
for o in out: print(f'  {o["name"]}: {o["written"]}')
