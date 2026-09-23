"""List tests that assert a verification failure while their App comes from TestApp.Create (mock signing)."""
import re, pathlib
root = pathlib.Path("/workspace/plang/PLang.Tests")
verify_touch = re.compile(r"signing\.verify|VerifyAsync|\.Verify\(|Signature|signature|Tamper|tamper|EnsureSigned|Sign\(", re.S)
uses_mock = re.compile(r"TestApp\.Create\(")
plain = re.compile(r"TestApp\.Plain\(|new global::app\.@this\(")
test_re = re.compile(r"\[Test\][^\n]*?\s*public async Task (\w+)\(\)\s*\{(.*?)\n    \}", re.S)
fail_assert = re.compile(r"IsFailure\(\)|\.Success\)\.IsFalse|IsFalse\(\)|Throws|Fail|Invalid|Reject|Mismatch|Expired", re.S)
fail_name = re.compile(r"Tamper|Fails|Invalid|Reject|Expired|Wrong|Mismatch|Forg|Replay|Stale|Untrusted|Unsigned", re.I)
for f in sorted(root.rglob("*.cs")):
    if "/obj/" in str(f) or "/bin/" in str(f): continue
    s = f.read_text(errors="ignore")
    if not (verify_touch.search(s) and uses_mock.search(s)): continue
    for name, body in test_re.findall(s):
        verifies = re.search(r"verify|Verify|Signature|signature", body) or re.search(r"verify|Verify", name)
        if verifies and (fail_name.search(name) or fail_assert.search(body)):
            mixed = " (file also uses Plain/new app)" if plain.search(s) else ""
            print(f"{f.relative_to(root)} :: {name}{mixed}")
