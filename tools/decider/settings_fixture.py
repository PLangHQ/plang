"""The C# Settings-block twin fixture — for a few goals whose steps name settings (and one that names none),
the Settings and Keys blocks python's prompt C shows (prompt_c.settings_block from settings.json,
prompt_c.keys_block). PickListTests
renders the same goals through the properties template and compares the block.

    python3 settings_fixture.py   → PLang.Tests/Wire/App/Decider/settings_golden.json
"""
import json, os
import prompt_c as c

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', 'PLang.Tests', 'Wire', 'App', 'Decider', 'settings_golden.json')

CASES = [
    ('Named', ['set default %!build.setting.cache% = true',
               'read notes.txt, cache=%!llm.setting.cache%, write to %notes%',
               'set %!app.test.setting.parallel% = 1',
               'set %!build.setting.files% = ["a.goal"]']),
    ('NoneNamed', ['write out %!data%', 'set %x% = %!goal.Name%']),
    ('Keyed', ['write out %!app.goal["/checkout"].path%', 'set %m% = %!app.module["file"]%']),
    ('SettingAndKeyed', ['set %!llm.setting.cache% = false', 'write out %!app.goal["/checkout"].name%']),
]

cases = []
for name, texts in CASES:
    goal = {'name': name, 'steps': [{'index': i, 'text': t} for i, t in enumerate(texts)]}
    cases.append({'goal': name, 'steps': texts, 'block': c.settings_block(goal) + c.keys_block(goal)})
json.dump(cases, open(OUT, 'w', encoding='utf-8'), indent=2, ensure_ascii=False)
print(f'{len(cases)} cases -> {os.path.relpath(OUT, os.path.join(HERE, "..", ".."))}')
