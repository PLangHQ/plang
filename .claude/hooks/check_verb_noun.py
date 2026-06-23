#!/usr/bin/env python3
import sys, json, re

d = json.load(sys.stdin)
file_path = d.get('file_path', '')
if not file_path.endswith('.cs'):
    sys.exit(0)

content = d.get('content', d.get('new_string', ''))
if not content:
    sys.exit(0)

violations = set()

# Multi-word PascalCase: two or more [A-Z][a-z0-9]+ segments
multi_word = re.compile(r'^([A-Z][a-z0-9]+){2,}$')
# Standard .NET naming conventions — always legitimate
excluded = re.compile(r'(Async|Exception|Error|Attribute)$')

# 1. Type declaration names (class/interface/record/struct/enum)
for m in re.finditer(r'\b(?:class|interface|record|struct|enum)\s+([A-Z][a-zA-Z0-9]+)', content):
    name = m.group(1)
    if multi_word.match(name) and not excluded.search(name):
        violations.add(name)

# 2. Member declaration names on lines with access modifiers (skip override — .NET contracts)
for line in content.splitlines():
    stripped = line.strip()
    if not re.match(r'(public|private|protected|internal)\b', stripped):
        continue
    if 'override' in stripped:
        continue

    # Method/constructor name: first identifier before ( that is NOT dot-preceded
    # Dot-access (.Foo()) = call site; space-preceded = declaration
    method_matches = re.findall(r'(?<!\.)([A-Z][a-zA-Z0-9]+)\s*\(', stripped)
    if method_matches:
        name = method_matches[0]  # first = declared name (return type has no ( after it)
        if multi_word.match(name) and not excluded.search(name):
            violations.add(name)

    # Property name: identifier immediately before {
    prop_match = re.search(r'([A-Z][a-zA-Z0-9]+)\s*\{', stripped)
    if prop_match:
        name = prop_match.group(1)
        if multi_word.match(name) and not excluded.search(name):
            violations.add(name)

if violations:
    fname = file_path.split('/')[-1].split('\\')[-1]
    print(f"🚩 Verb+Noun check blocked write to: {fname}")
    print()
    print("Multi-word PascalCase names found. Each needs one of:")
    print("  a) Rename to a single honest word (OBP fix)")
    print("  b) Add 'override' if this is a .NET interface contract")
    print("  c) Explicit justification why two words are necessary here")
    print()
    for v in sorted(violations):
        print(f"  {v}")
    sys.exit(2)
