# A self describing PLang app that an AI assistant can use

Written 2026-11-04 from work on a CRM app built with PLang. This is for whoever builds the next generation of PLang, so the lessons are not lost in a rewrite. The work was done and tested on a development server.

## The goal

Staff who are not programmers use an AI assistant on their own computer. Instead of clicking through the web UI, the assistant calls the same routes the UI calls. For that it needs three things from the app:

1. A way to find out what exists and what each route does (discovery).
2. A way to know what a route changes, so it knows when to ask the person first (effects).
3. A way to prove who is calling (identity), without a browser.

It is also a test for the future: any PLang app should be able to describe itself like this without extra work.

## Design decisions and why

### Discovery: `GET <path>?`

A GET whose raw target ends in `?` (nothing after it) returns markdown instead of running the route. The web server module handles it before any goal runs.

* `GET /admin?` lists the folders directly under `/admin` with the number of routes in each, and the routes that sit exactly at that level. It does not list the routes inside the folders.
* `GET /admin/crm?` goes one level down. `GET /admin/crm/mail/12/handled?` shows one route in full.
* All registered routes are visible, but only when that path is explored. This keeps each answer small, so the assistant drills down instead of reading everything.
* It requires a valid signature (see identity). An unsigned request gets 401.
* Opt in was rejected on purpose: every route is discoverable.

Where the text comes from, all read from the build output (`.pr`) at request time, nothing is parsed from goal text:

* The route list from the web server's registered routes (path, methods, path parameters).
* `Description` from the goal's `00. Goal.pr`. The runtime drops `Description` from memory when it loads a goal (`PrParser` sets it to null to save memory), so the docs read the `.pr` file again on demand.
* `IncomingVariablesRequired` from the same file, filtered to `%request.*%` values, merged with the same field of the sub goals.
* Effects, computed (below).

Code: `PLang/Modules/WebserverModule/RouteDocs.cs`, `GoalEffects.cs`, and a small hook in `RequestHandler.cs` (`IsRouteDocsRequest`).

### Why not the HTTP method as the safety signal

The HTTP method is not reliable in a PLang app:

* Many routes are registered for both GET and POST, because a form posts back to the URL it was rendered from and a GET only route would 404 (see the comment in `TryMatch`).
* GET routes write. Example: a route that refreshes a counter badge also runs a goal that marks items as handled.
* POST routes read. Example: searches.

So effects are computed from what the steps do, not from the verb.

### Effects: an attribute on the C# module methods

The person who writes `FileModule.WriteToFile` knows it writes. The docs generator should not carry a list of module names. So:

```csharp
[Flags] enum Effects { None, WritesData, WritesFiles, External, Costs, Opaque, FromSql }

[Effect(Effects.WritesFiles)]
public async Task WriteToFile(...)
```

* The attribute works on a class (default for every method) and on a method (overrides the class).
* `FromSql` means the effect depends on the SQL text of the step. Only for `QuerySqlFile` and `QueryDynamicSql`. The SQL is then read (from the step, or from the `.sql` file) and checked for INSERT, UPDATE, DELETE, REPLACE, CREATE, ALTER, DROP. This is the only text analysis, and it is SQL, not PLang.
* `QuerySqlFile` stays `FromSql` and is not marked as a read, because real apps use it to run an UPDATE. The `Execute*` family is `WritesData` directly.
* A method with no attribute and no class default reports `unknown`. It is never treated as harmless.
* The runtime finds the method with reflection from the `ModuleType` and `Function.Name` stored in each step `.pr`.
* The analysis walks the steps of the goal and follows any `call goal` (any `Path` ending in `.pr` inside a step parameter) down into sub goals, depth limited to 8, with a visited set. Results of the step parsing are cached by file write time.

What the person sees (English on purpose, the assistant translates):

```
read           changes nothing
changes data   creates, updates or deletes data, or writes files
external       contacts something outside the system, for example sends an email or calls another service
costs          uses a paid service such as an AI model
opaque         can do anything, be careful
unknown        what it does is not declared
```

Table names are not shown. The audience is a business user, not a programmer. They need to know "does this change anything, does it reach outside, does it cost", not which table.

Modules marked in this work: Db, File, Http, Llm, Terminal, Code, Plang, Inject, Environment, Caching, Image, WebCrawler, Message, ListDictionary, Convert, Html, Filter, Assert, Schedule, Cryptographic, Serializer, Identity, CallGoal, Loop, Variable, Conditional, ThrowError, Ui, Output, TemplateEngine, Webserver. Other modules still report `unknown`.

Known gaps:

* `External` is too coarse. "Sends an email to a person" is not the same as "fetches a web page". It cannot be told apart by module, because mail goes out through an HTTP POST. A real answer needs the app to say it, or a finer attribute on the sending goal.
* Bookkeeping writes (an AI usage log) count as "changes data". Noise.
* Opening a history list is tagged `changes data; costs`. That is true: loading it triggers an AI analysis of unscanned mail in the background. Nobody saw this from the UI.

### The description: what the caller gets, in one sentence

`Description` already existed in every `.pr` and nothing used it. Rather than add a new field (which would break every `.pr` and the C# model), its content was changed:

* First sentence: what the caller gets or what changes for them, in the words of the business. Never how: no file names, SQL, variables, templates, step order.
* The rest: conditions and details.
* The docs list uses the first sentence, the detail view uses all of it.

Left alone, the model only guesses from the steps and often gets it wrong or describes the implementation ("Marks the badge as completed" for a goal that refreshes a counter). Domain knowledge is not in the steps.

So the author states it, in the goal, above the goal name:

```
/ Marks all unhandled incoming mail of one company as handled, which removes it from the work list when nothing else is open.
MarkMailHandled
- query file "markMailHandled.sql", companyId=%id%, by=%user.id%
...
```

* A comment above the goal name is the goal's `Comment`. A comment under the name attaches to the first step instead, which is not what we want.
* The builder puts the comment in front of the description word for word (`GoalBuilder.WithAuthorComment`), and takes out any copy of it the model wrote into the details. Asking the model to copy it was not reliable: it repeated it, or copied the leading slash.
* Only route goals get a comment. Helper goals keep what the builder writes.
* A glossary file maintained by hand was rejected: it is a second source of truth that goes stale and is not builder knowledge. The comment lives next to the code it describes and shows up in the diff.

### Identity: sign with openssl, no browser

PLang already signs requests with Ed25519 and verifies them on the server (`PLangSigningService`). Nothing new was needed on the server, except the bug below. The assistant can produce the same signature with `openssl` and `curl`:

* The header is `X-Signature` and its value is raw JSON (not base64).
* The JSON, in this exact property order, with `null` written out and the date as `yyyy-MM-dd'T'HH:mm:ss.fff'Z'`:

```
{"type":"Ed25519","nonce":"<uuid>","created":"<utc>","expires":null,"name":null,"data":null,"contracts":["C0"],"headers":null,"parent":null,"identity":"<base64 raw public key>","signature":null}
```

* The signature is Ed25519 over exactly those bytes (the same JSON with `"signature":null`), base64 encoded, then put into the `signature` field of the same JSON. The server re-serializes the message the same way when it verifies, so any difference in order or spelling breaks it.
* `identity` is the raw 32 byte public key in base64.
* The server rejects a signature older than 5 minutes and a nonce it has seen in the last 5 minutes.
* The key pair is made once: `openssl genpkey -algorithm ed25519`. The person only needs to give the app their public key. The private key never leaves their computer.
* Mapping a key to a person is up to the app. In the app this was tested on, an identities table has a `userId` column, and setting it for the public key gives the caller that user's rights, the same as in the browser.
* Binding the body: put `"data":{"type":"sha256","format":"json","hash":"<lowercase hex sha256 of the raw body bytes>"}` in the signed message instead of `null`. The server reads the hash type from the signature (`Data.Type`), and the Cryptographic module's `Hash` supports `keccak256` and `sha256`, so a client does not need Keccak256. Tested: the untouched body is accepted (200), a body changed after signing is rejected (400, "Data does not match in value to received signature"). A request signed with `"data":null` is accepted without checking the body, so a server that allows writes from outside should refuse a write whose signature has no `data`.

Recipe (works with OpenSSL 3.0; `-rawin` needs a file, not a pipe, in 3.0):

```bash
KEY=~/.myapp/key.pem
[ -f $KEY ] || { mkdir -p $(dirname $KEY); openssl genpkey -algorithm ed25519 -out $KEY; chmod 600 $KEY; }
PUB=$(openssl pkey -in $KEY -pubout -outform DER | tail -c 32 | base64 -w0)
NONCE=$(cat /proc/sys/kernel/random/uuid)
CREATED=$(date -u +%Y-%m-%dT%H:%M:%S.000Z)
DATA=null     # for a request with a body:
# HASH=$(printf '%s' "$BODY" | openssl dgst -sha256 -hex | sed 's/^.*= //')
# DATA="{\"type\":\"sha256\",\"format\":\"json\",\"hash\":\"$HASH\"}"
MSG="{\"type\":\"Ed25519\",\"nonce\":\"$NONCE\",\"created\":\"$CREATED\",\"expires\":null,\"name\":null,\"data\":$DATA,\"contracts\":[\"C0\"],\"headers\":null,\"parent\":null,\"identity\":\"$PUB\",\"signature\":null}"
TMP=$(mktemp); printf '%s' "$MSG" > $TMP
SIG=$(openssl pkeyutl -sign -inkey $KEY -rawin -in $TMP | base64 -w0); rm -f $TMP
SIGNED="${MSG/\"signature\":null/\"signature\":\"$SIG\"}"
curl -sS -H "X-Signature: $SIGNED" "https://app.example/admin?"
# with a body: curl -sS -X POST -H "X-Signature: $SIGNED" --data-binary "$BODY" "https://app.example/..."
```

Idea not done: a device flow like `gh auth login`, where the person approves a code in the browser they are already logged into. Not needed, the signed request is simpler and the key never moves.

## Bugs found on the way (fix in the rewrite)

1. **A failed signature check was swallowed.** In `RequestHandler.HandleRequestAsync`, `ParseRequest` was assigned to the same `error` variable, which overwrote the signature error with null, and the request went on as anonymous with a 200. Fixed with a separate variable, and the "Signature is invalid" error now carries status 401.
2. **The batched description request never saw the goal comment or name.** `BatchedInstructionBuilder` sent only the step texts, so the model could not use a comment and gave step level descriptions. It now sends the goal name, the goal comment and step comments.
3. **An unchanged goal is skipped before its description is looked at.** A new description style does not reach existing goals until the goal text changes. `PLangRedescribe=1` bypasses the check inside the description step, but only together with `--rebuild`, because the whole goal is skipped earlier. A description only mode (no step rebuild) would be the right tool.
4. **`--rebuild` is noisy.** It rebuilds every step of the goal file and rewrites `.pr` files with refreshed `SetupHash` and timestamps. Adding a comment and building normally is enough and reuses unchanged steps.
5. **Builds must not run in parallel** in the app tested. A pass over many goals is a sequential script.

## What to carry into the next generation

* Make the effect a first class, declared property of an operation (module method), not something a docs tool guesses. Default to "unknown", never to "safe".
* Compute the effect of a goal from its steps and sub goals at build time and store it in the build output, instead of at request time. The runtime then drops nothing it needs (this version dropped `Description` from memory, which forced a re-read of the `.pr`).
* Keep a one sentence, caller facing summary as its own field from the start, and let a comment above the goal set it by hand. Do not make the model rediscover domain meaning from steps.
* Discovery by a plain `?` on any path is cheap and works for any app. Folder first, details on demand.
* Identity: keep signing on Ed25519 with a documented, exact byte format. The format is the part an outside client has to reproduce, so write it down and keep it stable. Cover the body with a hash that standard tools can produce (SHA 256 works today), and consider refusing a write whose signature has no body hash.
* Do not use the HTTP method to decide whether a call is safe.
* The audience is a business user plus an assistant. Anything shown should be in their words (changes data, costs, external), not table or file names.

## Commits in this repo

`fe9680a` (docs and 401), `c9cffe3` (Effect attribute), `1a2eff2` and `6158b74` (description rules and author comment), `2d7877d` (English effect tags).

## Still open

* Finer `External` (mail to a person), filter bookkeeping writes.
* A description only rebuild mode.
* Refuse a write from outside when its signature has no body hash.
* How a new person's public key gets registered.
