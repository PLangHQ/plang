# Cool — things PLang's architecture enables that other languages can't easily do

Forward-looking ideas. Not committed, not necessarily near-term. Here because PLang's primitives (signing, callbacks, snapshots, identity, goals, channels) combine to enable capabilities that would take other languages months of framework work — and we want to remember they're possible.

## Channels that migrate across devices

**The combination:** Snapshots + signed identity + suspend/resume + channels-as-state.

**The capability:** A Session channel can be lifted, serialized into a snapshot, transported to another device, and resumed there with the conversation state intact. Not session replication where two clients share state — the *channel itself* moves, with cryptographic continuity proving it's the same channel.

**Concrete:** "I started a chat on my phone, picked up on my laptop. The channel migrated; my counterpart never saw a disconnect." The channel.migrate action snapshots the channel + its identity, signs it, ships it; the target's identity-aware runtime accepts and resumes.

**Why nothing else does this:** Erlang has process migration but no signing. WebRTC has session resumption but no identity continuity. PLang has both as language primitives.

**What it would take:** a `channel.migrate` action that bundles channel state + signing + transport. Plumbing exists in snapshot infrastructure; needs a packaging surface.

(Tracked in `runtime2-channels` plan as a deferred stage.)

## Channels as smart contracts (no blockchain)

**The combination:** Goal channels + persistent goals (Settings store) + signed I/O + snapshots.

**The capability:** A channel encodes a multi-party agreement. "Writes from Alice trigger goal X; writes from Bob trigger goal Y; either party can read the agreed outcome; if either reneges, the snapshot proves it." All in PLang code, with cryptographic provenance, without a blockchain.

**Concrete — escrow channel:** Alice writes payment, Bob writes goods, the channel goal verifies both signatures and releases. If Bob doesn't deliver, Alice's snapshot of the channel state proves it for arbitration. The snapshot is the dispute record.

**Why nothing else does this:** Ethereum + Solidity can express it but at the cost of a chain, gas, public ledger, deployment cycle. PLang gets the same integrity guarantees from local primitives; appropriate when both parties are willing to trust the snapshot infrastructure (most B2B / enterprise scenarios).

**What it would take:** nothing new. It's a pattern, not a feature. Worth documenting once a concrete use-case lands.

## Sudo-for-I/O — channels gated by human authorization

**The combination:** Goal channels (or events) + `ask` + suspend/resume + signed identity.

**The capability:** A sensitive write pauses out-of-band for human approval before completing. The approver may be in a different process, on a different device, responding via a different channel (Slack, email, in-app prompt). Approver's signature attaches to the write. Suspended state survives restarts; the write resumes when authorization arrives.

**Concrete:** Before logging PII to an external service, route through a goal channel (or fire a before-event) that asks a compliance officer to approve. Officer pings yes/no via their own channel; if yes, the byte goes through with both signatures (writer + approver) attached; if no, the write fails. Audit trail is automatic via snapshots.

**The pattern (composition, no new infrastructure):**

```
ApproveAndWrite:
- ask ComplianceOfficer "Approve %!data%?", write to %ok%
- if %ok% = "yes" then
  - write %!data% to ExternalLog
- otherwise
  - throw "denied"

- set audit.external channel as ApproveAndWrite
```

Or via events — bind a before-write event on the sensitive channel that does the ask before the write proceeds.

**Why nothing else does this:** suspending an in-flight write across days, restarts, and process boundaries needs callback infrastructure. PLang has it as a language primitive; other languages need a workflow engine (Temporal, Airflow) bolted on.

**Note:** this is a *teaching pattern*, not a runtime feature. The runtime offers no `Authorizer` config — gating happens via composition (goal channel or events). Discipline-based, not enforced. The benefit is no new surface; the cost is "remember to wire it." For genuinely-sensitive operations, document it carefully.

## Causal lineage — `Data.Causes` as a DAG

**The combination:** Typed `Data` properties on action handlers + source-gen + variable-template resolution.

**The capability:** every Data carries a `Causes` field — references to the parent Datas it was derived from. The graph is a DAG (multi-input, multi-output). Walk back through Causes and you have a verifiable trace of which inputs produced any output, with each link cryptographically pinned by existing signing.

**Concrete — explainable credit decision:** A loan goal pulls a customer record, fetches a credit score, queries recent transactions, computes a risk profile, decides approve/deny. Six months later the customer asks "why was I denied?" Walk `Decision.Causes → Risk → {Score, Tx} → {Customer}`. Each node was signed when produced. The graph *is* the audit trail — the auditor doesn't have to trust the bank's narrative; they can verify the derivation independently.

**Same shape applies broadly:**

- "Where did this customer's email end up?" — walk Causes forward; every system it derived through is in the graph.
- "Which model predictions used the now-corrected data point?" — find Datas with that point in Causes.
- "Recompute only the report parts that depend on the corrected input" — invalidate forward through Causes, smart caching for free.

**What it would take:**

- Add `Causes: List<Data>` to `Data.@this`.
- Source-gen: when an action handler runs, set `result.Causes = [each input Data property]` automatically. Handler writes nothing extra; it falls out of the existing typed-Data shape.
- Variable-template resolution: when `"http://.../%user.ssn%"` resolves, the resulting Url Data attaches Causes from the variables it referenced. Modest change in the lazy-resolution path.

**Why nothing else does this:** application code typically doesn't track data lineage. Spreadsheets do (formulas reference cells), build systems do (Make, Bazel, dependency graphs), but general-purpose programs don't — every framework that adds it (Apache Airflow, Dagster, etc.) sits *outside* the language. PLang would have it inside, automatic, signed.

**Distinct from chained signatures:** signature chains record *who* handled the byte sequence (custody). Causes records *what inputs produced* the byte sequence (lineage). Orthogonal, both useful, both express different parts of "show your work."

## Counterfactual production replay

**The combination:** Snapshots capture full App state including channels, Variables, CallStack. Goals are deterministic given inputs.

**The capability:** Take a production snapshot from any moment. Re-run from there but inject *different* inputs at any step. See what would have happened. Not "rerun the same trace" (debugging) — *fork* production state and follow a counterfactual.

**Concrete:** "Customer X was rejected at step 5. What if we'd accepted? Re-run from that snapshot with `decision=accept`, see the downstream effects, decide whether to issue a manual override before the customer notices." Or A/B testing on past decisions: "what if we'd routed traffic differently last Tuesday?"

**Why nothing else does this:** would need event sourcing across the entire stack. Most systems can't even produce a coherent multi-component snapshot.

**What it would take:** a `snapshot.fork` action and the ability to inject `Variables` overrides at resume. Snapshots already capture the rest.
