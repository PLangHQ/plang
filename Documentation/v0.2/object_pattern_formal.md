# The Object-Based Pattern (OBP)

OBP is a pattern. It knows three things — a root, a context, and data flowing through methods — and nothing outside its own world. This document is language- and project-agnostic; the operational catalog of violations with worked examples lives in [`obp-smells.md`](obp-smells.md).

## The Problem: Friction

Consider what happens when you save user data in a traditional codebase. A controller receives a request. It extracts fields. It passes them to a service. The service validates, transforms, calls a repository. The repository maps to an entity, calls a database. Every layer knows the shape of the data. Every layer has parameters threaded through it. Every layer adds code, adds coupling, adds CPU cycles.

That is friction. Every parameter pass, every service layer, every eager load, every abstraction that exists "just in case" — friction. Wasted CPU. Wasted complexity. Wasted context for anyone reading the code, human or LLM.

Traditional architecture says: know everything, everywhere. Every method signature declares exactly what it needs. Every constructor loads what it might use. Every layer transforms data into its own shape.

OBP says: stop. Most of that work is unnecessary.

### Lazy everything

```csharp
var path = new Path("/file.txt");
path.Size  // how big is the file?
```

Traditional code loads size, dates, and attributes in the constructor — work nobody asked for. In OBP, `new Path("/file.txt")` stores only the string. When you access `path.Size`, *then* it does the work. Only the CPU that's needed, only when it's needed. Construction is simple; knowledge is navigated.

### No null checks

A big part of OBP is making code simpler, and nothing pollutes code like defensive null checks — every one is friction, and every one is a place to get it wrong. OBP designs them out: references are non-nullable by construction, so an object can always navigate. A type whose consumers must null-check it before every use is a type born incomplete — fix the birth, not the call sites.

## The Insight: Data Flows Blind

A program reads a file into a variable, stores the variable in a database, writes it out. The caller has no idea what's in the variable — it's just there, it flows. This is not a limitation; it's the design principle. Only the endpoint that actually works with the data needs to know its shape.

And if only one place knows the shape, then that place must own all the logic. There's nowhere else to put it.

That single insight — **only the owner knows the shape; everything else relays** — is where every law and rule below comes from.

## The Three Laws

The laws are unbreakable. Without any one of them, whatever you have, it is not OBP. Everything else in this document is a rule — rules bend with judgment; laws don't.

### Law 1 — There is a root object

One root; everything is reachable from it by navigation. Any code holding the root can reach anything — no dependency-injection framework, no service locator, no parameter lists that grow every time you need one more thing.

```csharp
app.Channel.Write(text);
app.Cache.Get(key);
```

Read it like English: "the app's channel — write." The code tells you exactly what it's doing and where it lives. The object graph IS the architecture.

### Law 2 — There is a context, and it belongs to the request

Each request has a context. The context has access to the root. It is how an object reaches beyond itself — to the root, to the request's state — without anyone threading parameters through every signature in between.

### Law 3 — Data flows through all methods

Data moves through the system as one opaque unit. It carries the variable; nobody in transit knows what's inside, and nobody cares. Every layer between the origin and the endpoint has the same one signature regardless of payload — that is what keeps the pipeline short. The only code that looks inside is the leaf that owns the value.

## The Rules

Rules derive from the laws and can be broken — a considered exception survives; the pattern doesn't survive a broken law. Each rule is named; use the names.

### The owner does the work

Every operation belongs to the object whose data it acts on. If it iterates a collection, it belongs on the collection. If it transforms a result, it belongs on the result type.

**Test**: "Whose data does this method touch?" That's the owner.

Parents delegate; they never iterate their children:

```csharp
public async Task Load(Context context)
{
    await Lifecycle.Before.Run(context);
    await Step.Load(context);              // delegates, does not loop
    await Lifecycle.After.Run(context);
}
```

### Navigate, don't pass

Reach dependencies through the object graph. Never decompose an object into separate parameters:

```csharp
// Wrong: passing each thing separately
async Task Run(App app, Channel channel, Cache cache) { }

// Correct: reach them through the root
async Task Run(App app)
{
    app.Channel ...
    app.Cache ...
}
```

This applies to the caller too. If a handler calls `path.Delete(recursive, ignoreIfNotFound)`, it's decomposing itself into parameters. The OBP form is `path.Delete(request)` — let the callee navigate the caller's object for what it needs. Coupling stays one-directional: if the callee needs a new property later, only the callee changes.

### The name is the contract

You navigate the graph by name; the name alone must tell you what you'll find.

- **Properties and types are one honest noun.** The name says what the thing IS. Property-shaped knowledge is a property: `Count`, never `GetCount()`.
- **Methods are one verb naming the caller's intent** — `Open`, `Read`, `Write`, `Close`, `Get`. The verb says what the caller is doing, never the mechanism behind it: as the developer holding the instance you just want the thing — `cache.Get(key)`, not `cache.Resolve(key)`. If the verb describes how the answer gets made, it's the wrong verb.
- **Boolean questions may compound: `IsX` / `HasX`.** This is the only sanctioned compound.
- **Verb+noun is never allowed.** `BuildTypeEntries`, `GetParameters`, `CoerceToKind` — a compound where one half is a verb is not a style problem, it's a design alarm: the object is doing another object's job, or building/proxying something that should own itself. The name is how the bad design surfaces in the API.

**Test**: if the name could describe two different things, it's too broad. If you need two words, you haven't found the right one.

### Keep the reference

Store the object, not extracted fields. Store `Step`, not `step.Text`. Decomposing objects into primitives discards information, and the flat copy drifts when the source changes. Wrapper DTOs exist only at serialization boundaries.

### The collection is the API — and it is its own type

An owned collection is its own type, living under the concept it collects — `error.list` — and it owns its discipline: private backing store, its own `Add`, locking inside. A bare `List<T>`/`Dictionary` never appears as public state; the moment its add/lock/evict rules live at call sites, the discipline has left the owner.

The owner exposes the collection as a **singular** property naming the concept — the property names the concept node; the type says it's a list:

```csharp
public class CallStack
{
    public error.list Error { get; } = new();
}

callStack.Error.Add(error);      // "the callstack's errors — add this one"
callStack.Error.List             // enumerate
```

And the owner never proxies it. `callStack.AddError(...)` is wrong in every world — a middleman hiding what's actually happening. When the collection needs domain operations (`Load`, `Run`), those belong on the collection type — still never on the parent.

Bare collections are fine as transient locals, private backing fields, and DTO fields at serialization boundaries.

### Reaching out requires context

An object that needs to reach outside itself receives a context (Law 2). Preferred: **born with it** — a private, non-nullable field set at construction. Born non-nullable, the object can always navigate; no null checks anywhere (see philosophy). Constructing context-less and stamping later is the smell — the gap where the incomplete object exists is where the bugs live.

Receiving context later — as a method parameter — also works; it's the fallback, not the default. Concurrency is not an argument against storing: async-local state flows the current request's scope where a stored field can't.

### Data rides sealed

Data is created once, at the boundary where the value originates. From there it relays whole — every layer between origin and endpoint passes it unchanged. Couriers may read routing and success/error state; they never open the package. A copy or a retype carries the already-built value as-is — it is not a second origination, so nothing gets re-parsed, re-resolved, or re-typed.

Only leaves open the package: the handler that declared the typed slot, and the serializer that renders the value for a format. And even a leaf reads its own value without cracking it: operations live on the value, and other operands ride in as whole carriers.

```csharp
// Wrong: courier opens the package mid-flight
if (input.Value is Image img) return DoSomething(img.Bytes);

// Wrong: leaf cracks the carriers open for a static helper
return Resize((await A.Value()).Bytes, await Width.Value(), await Height.Value());

// Correct: the value does the work; operands pass whole
return await A.Resize(Width, Height);
```

The tell: you extracted a value only to hand the raw inside to something else. If you opened the box to pass what was inside, pass the box.

### No redundant wrappers

If the data a callee needs already exists on an object the caller has, pass that object. Don't create a new class that copies fields into a different shape — every wrapper is a second version of the truth.

### Never diverge

When two cases can follow the same pattern, they must. There are no "this case is simple enough to handle inline" carve-outs: a closed set today is not closed tomorrow, so the exception rots — and uniformity IS discoverability. Finding the behavior for any case must never require a hunt; it's always in the same place the pattern puts every other case. Two patterns for one thing means the code diverges over time; that is the danger zone.

### Registries select; elements behave

A registry's whole job is selection and lifecycle. All behavior lives on the element. A type-check inside a registry (`is X.subtype` → behave differently) is misplaced behavior — push it onto the element as a virtual member. The generic fallback path sitting beside per-element handlers is the same mistake: every element gets its own uniform implementation instead.

### A method holds its own logic

A method contains its logic inline. Extract a helper only when a second caller actually exists. A handful of lines inline beats a private helper the reader must jump to — when you read the method, you have the full answer in one place. Extraction also tends to hide a dispatch that belongs on an object: the helper's body is often a type-switch that should be a virtual member on the value.

### Cost never justifies decomposing

Performance and allocation arguments never license opening the box — storing a raw value instead of the carrier, stripping the envelope for speed. The cost is real; the answer is always a non-decomposing one: make the carrier itself lighter, or find a structural model that stores it whole.

## Why This Matters for LLMs

An LLM reading OBP code traverses the object graph like a map: it knows exactly where everything lives because names are contracts and behavior sits on its owner. Traditional architecture scatters behavior across services, managers, and utilities — the reader must hold the entire service graph in context to understand one operation. More context, worse results. OBP collapses that context: navigate to what you need, read it like English, every object does its own work.

## The Smells

Violations have names — *naked collection*, *broken seal*, *verb+noun*, *fork*, *behavior class*, and the rest. The catalog with worked examples and grep-able tells is [`obp-smells.md`](obp-smells.md); the audit procedure for changed code is [`obp-scan.md`](obp-scan.md).
