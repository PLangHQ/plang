"""SourceFix check — does os/system/builder/llm/SourceFix.llm get the programmer's fix right?

Sends what BuildGoal/Start.goal SourceError sends (system: SourceFix.llm; user: "<key>: <message>\n\nThe
goal:\n<goal source>"), the way OpenAi.cs sends a query with no schema (temperature 0), and compares
the answer with the corrected line. Raw answers land in runs/source_fix_<stamp>.json.

    python3 source_fix_check.py
"""
import json, os, re, time, urllib.request
import build_pr as b

HERE = os.path.dirname(os.path.abspath(__file__))
SYSTEM = open(os.path.join(b.ROOT, 'os/system/builder/llm/SourceFix.llm'), encoding='utf-8').read()
MODEL = os.environ.get('MODEL', 'gpt-5.4-nano')

CASES = [
    {'id': 'else_line_with_indented_bodies',
     'goal': 'OneOrOther\n- if %x% == 1\n    - write out "one"\n- else\n    - write out "other"',
     'error': 'ElseWithoutIf: step 2 "else" — an else must be in the same step as its if.',
     'expected': '- if %x% == 1, write out "one", else write out "other"'},
    {'id': 'else_line_with_inline_body',
     'goal': 'Proceed\n- if %ok% is true, call Continue\n- else call Stop',
     'error': 'ElseWithoutIf: step 1 "else call Stop" — an else must be in the same step as its if.',
     'expected': '- if %ok% is true, call Continue, else call Stop'},
]

def ask(user):
    body = json.dumps({'model': MODEL, 'temperature': 0.0, 'max_completion_tokens': 16000,
                       'messages': [{'role': 'system', 'content': SYSTEM}, {'role': 'user', 'content': user}]}).encode()
    req = urllib.request.Request('https://api.openai.com/v1/chat/completions', data=body,
                                 headers={'Authorization': f'Bearer {b.OPENAI_KEY}', 'Content-Type': 'application/json'})
    with urllib.request.urlopen(req, timeout=300) as r:
        return json.loads(r.read())['choices'][0]['message']['content']

def norm(t):
    return re.sub(r'\s+', ' ', t.strip()).strip()

if __name__ == '__main__':
    out = []
    for c in CASES:
        answer = ask(f'{c["error"]}\n\nThe goal:\n{c["goal"]}')
        ok = norm(answer) == norm(c['expected'])
        out.append({**c, 'model': MODEL, 'answer': answer, 'match': ok})
        print(f'{c["id"]:<34} {"YES" if ok else "NO"}\n   got: {answer!r}')
    path = os.path.join(HERE, 'runs', 'source_fix_' + time.strftime('%Y%m%d_%H%M%S') + '.json')
    json.dump(out, open(path, 'w'), indent=1, ensure_ascii=False)
    print('wrote', path)
