"""The C# confirm twin fixture — the builder's ConfirmNumbers: for a set of unwritten numbers (prompt_c.unwritten),
the decider's state and questions as python sends them. PLang.Tests' ConfirmTemplateTests renders confirm.state.template
and confirm.template for the same numbers and compares.

    python3 confirm_fixture.py   → PLang.Tests/Wire/App/Decider/confirm_golden.json
"""
import json, os
import prompt_c as c, formal as f

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', 'PLang.Tests', 'Wire', 'App', 'Decider', 'confirm_golden.json')

# (step index, step text, the answer's formal for it)
CASES = [
    (1, 'call Flaky, on error retry once, ignore', 'goal.call(Name="Flaky"); on.error(RetryCount=1, Ignore=true)'),
    (3, 'read %path%, cache for a minute', 'file.read(Path=%path%); on.cache(Duration="PT1M")'),
    (4, 'call Save, on error retry twice over 10 seconds', 'goal.call(Name="Save"); on.error(RetryCount=2)'),
]

numbers = [u for i, text, formal in CASES for u in c.unwritten(i, text, f.parse(formal))]
json.dump({'numbers': numbers, 'state': c.confirm_state(numbers), 'questions': c.confirm_questions(numbers)},
          open(OUT, 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
print(f'{len(numbers)} numbers -> {os.path.relpath(OUT, HERE)}')
for n in numbers: print(f'  {n["id"]}')
