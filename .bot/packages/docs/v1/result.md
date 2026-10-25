# docs v1 — packages: changes
- docs/modules/crypto.md: keccak256 is BouncyCastle KeccakDigest(256), original Keccak padding.
- Documentation/Runtime2/data-spec.md: removed the deleted Newtonsoft JToken unwrap line.
- CLAUDE.md "Speed record": bench runs the parts the diff touches; rests only for store/pool/held-memory diffs; no OpenAI.
- Documentation/v0.2/apps.md: same bench facts; csproj package lines 69–81.
- Not documented: screen.setting.max (128 MiB, --screen.setting.max=1GiB) lives in XML docs (screen/setting/this.cs:12) only; a module-page line would be a teaching edit that re-pins goldens.
