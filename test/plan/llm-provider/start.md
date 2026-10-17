# llm-provider

**Status: design complete, parked (2026-10-17, decision 864).** Ingi: "lets finish the design on this branch so we have it when we come back, then move to branch A". Nothing is built. It's built after branch A, and uses two things branch A makes:
- a type born from the list, so it knows its kind;
- a type answering its own JSON schema (813).

Before building, re-read the plan against branch A's result, and get Ingi's go.

Traces to: the conversation with Ingi on 2026-10-17, logged in `.bot/kind-is-type/architect/summary.md` as decisions 851 and 852. From here on, decisions go in `.bot/llm-provider/architect/summary.md`. The branch is cut from llm-writer (feaa3a7cd), with llm-writer's old stage 3 reverted (b368e6901), so it starts from OpenAi as decision 819 left it. llm-writer itself doesn't move; it continues its stage 3 on this branch's shape once this merges back.

## Why

llm-writer's stage 3 (how `%image%` reaches an llm) couldn't be built: there was no right place for it in the provider. `OpenAi.Query` is one method of about 475 lines (`llm/code/OpenAi.cs:59–533`) that does every job in order:

- settings;
- turning the messages into a C# List (`:85`) and keeping them C# to the end;
- rendering;
- copying the messages;
- the format instruction;
- the cache;
- tools;
- the request body as C# dictionaries;
- http;
- reading the reply;
- the tool loop;
- validation;
- the stored conversation;
- the response properties.

Sixteen static helpers each do another object's job. The llm key, endpoint and model sit in the shared llm setting, so a second provider has nowhere to put its own.

Ingi: "big failure there on `.Clr<List<LlmMessage>>()`, message should be plang type, the list should be plang list type … it should not become clr until the last step it's needed, basically constructing the json string", and "if this action is so wrongly setup, we cannot construct our code correctly".

## What the developer gets

The same steps, the same answers. This is a shape change:

- every llm test stays green;
- the builder's prompts stay byte-equal.

What the developer does notice:

- **The next provider is a folder** beside `llm/openai/`.
- **A missing key is asked for once, as today,** but the setting asks, not the llm.

## The rulings so far (Ingi, 2026-10-17)

1. **Data flows as far as it can** (851): a carrier is passed on whole and opened only where its value is needed. Ingi: "it's not the responsbility of the Query class to materialize the value, you are only doing that because you know the subclass need that". This replaces decision 231 ("handlers open carriers"); a CLAUDE.md proposal is filed.
2. **One loop, no provider in it.** Each provider is only its wire, in its own folder (Ingi: "it should be called llm/code/default.cs, and we then have the openai format … then others can plugin"). Revised in 859: the loop is the conversation's own `Run`, and the provider is the `[Code]` slot's `ILlm`, chosen by `code.setDefault`, with only `Ask` (Ingi: "ok, looks good"). There's no `Default.cs`.
3. **A value formats itself** (Ingi: "who is responsible for formatting a value. Answer: the value is responsible. so why are seeing a variable called format there?"). The loop holds no format.
4. **The conversation puts itself together** (Ingi: "why doen't conversation handle the sticting to gether the conversation"). It holds only its messages, never the query that made it (Ingi: "why is borned with query?").
5. **`Conversation` holds the earlier answer it continues:** `continue the conversation in %first%` builds `Conversation = %first%`. There's no `{continue}` record, no `Follow`, and no `"Messages"` property read by name. How the answer holds its conversation comes later (Ingi: "lets get back to this later").
6. **`Ask` takes the action and the conversation, and answers data:** an answer, or why there is none. There's no `request` type (Ingi: "why does Ask not just take in actoin and conversation?"; "what if there is an error, we dont handle errors?").
7. **The key, endpoint and model are the provider's own setting** (`llm/openai/setting/this.cs`, `%!llm.openai.setting.key%`). The shared llm setting keeps only what every provider shares (Ingi: "it should not be llm/settings/this.cs for openai, what happens for the next llm?").
8. **A setting asks for itself** (Ingi: "a setting should ask by it self if key is missing, it should not be done by the higher level class that uses it" → "yes better"). An option marked `[Ask("…")]` that is empty when read through the setting's door is asked for once and saved. This is a locked addition (the setting type and a new attribute) with Ingi's yes. `llm.setting.Asked` goes.

9. **A setting is read through one async door, and only when used** (Ingi, 854: "setting should always be await/async, but why is it .Of, why not .Get?"; "value should only be retrieve/load/materialized, on usage" → "yes"). `await context.Setting.Get<T>()` answers `data<T>`. On first use it reads that setting's row by its path, asks for an empty `[Ask]` option, and answers a bad row or a key nobody gave as an error, not a throw. App start reads no settings; the line at `app/this.cs:406` goes. `Of<T>()` (`actor/setting/this.cs:310`) goes, and its 29 callers move to `await …Get<T>()`. This is a locked addition (the settings' surface), with Ingi's yes.

## The design so far

```
llm/query.cs                  the action: its slots; Start, one line: the conversation runs (stays one line)
llm/code/ILlm.cs              the [Code] slot: the provider, its Ask; chosen by code.setDefault (859)
llm/type/conversation/        the conversation: its messages, and its Run, the loop
llm/type/answer/              NEW: one reply: content, tool calls, usage, its conversation
llm/type/usage/               NEW: tokens and cost
llm/type/tool/                NEW: a tool (action.cs, goal.cs) and the step's tool list
llm/openai/this.cs            NEW: OpenAI's provider, ILlm: Ask, Read, Stream
llm/openai/setting/this.cs    NEW: OpenAI's key, endpoint, model, timeout
llm/setting/this.cs           shared: length, rows, inline, cache
```

The provider plugs in through the `[Code]` slot (859). plang already picks an implementation there with `code.setDefault` (`code-modules.md:116`), so `ILlm` is the provider and its only job is the wire. The loop is the conversation going on until it's done; it isn't a provider's, so it would be copied by every provider if it lived there. `Default.cs`, sketched earlier, goes away.

```csharp
// llm/code/ILlm.cs: the provider (openai by default)
public interface ILlm : ICode
{
    Task<data.@this<answer.@this>> Ask(query action, conversation.@this conversation);
}

// llm/query.cs: still one line
public async Task<data.@this> Start() => await new conversation.@this(Message, Conversation).Run(this);

// llm/type/conversation/this.cs: the loop, with no provider in it
public async Task<data.@this> Run(query action)
{
    while (true)
    {
        var asked = await action.Llm.Ask(action, this);         // whichever provider the [Code] slot holds
        if (!asked.Success) return asked;                       // the call failed, or the model stopped early
        var answer = await asked.Value();                       // opened here: the loop decides on it
        Add(answer.Message);

        if (answer.Tool.Count > 0)                              // the model calls tools (857)
        {
            var called = await action.Tool.Use(tools => tools.Call(answer, action.Limit, this));
            if (!called.Success) return called;
            continue;
        }

        var checked = await answer.Check(action.OnValidateResponse, action.Limit, this);   // the step's check (858)
        if (!checked.Success) return checked;
        if (checked.Retry) continue;

        return asked;                                           // the answer, holding its conversation
    }
}
```

```csharp
// llm/type/conversation/this.cs
public sealed class @this : item.@this
{
    private readonly data.@this<list.@this<LlmMessage>> _message;   // the step's messages, unread
    private readonly data.@this<answer.@this>? _earlier;            // the answer it continues, unread
    private readonly list.@this<LlmMessage> _added = new();         // what the rounds add

    public @this(data.@this<list.@this<LlmMessage>> message, data.@this<answer.@this>? earlier = null)
    {
        _message = message;
        _earlier = earlier;
    }

    public void Add(LlmMessage message) => _added.Add(message);

    // written out: the earlier conversation, the step's messages, then what was added. Read only now.
    public override async ValueTask Output(IWriter writer, View mode, actor.context.@this? context)
    {
        writer.BeginArray(-1);
        if (_earlier is not null) foreach (var m in (await _earlier.Value()).Conversation) await m.Output(writer, mode, context);
        foreach (var m in await _message.Value()) await m.Output(writer, mode, context);   // each renders itself (llm-writer #2)
        foreach (var m in _added) await m.Output(writer, mode, context);
        writer.EndArray();
    }
}
```

```csharp
// llm/openai: Ask, the only I/O. OpenAI's field names, its endpoint, its key.
private async Task<data.@this<answer.@this>> Ask(query action, conversation.@this conversation)
{
    var setting = await action.Context.Setting.Get<llm.openai.setting.@this>();   // read on use; an empty key asked for once
    if (!setting.Success) return setting;                                         // no key given: MissingLlmKey
    var openai = await setting.Value();

    var body = new dict.@this()                                      // carriers in, read when the body is written
        .Set("model", action.Model)
        .Set("messages", conversation)
        .Set("temperature", action.Temperature)
        .Set("top_p", action.TopP)
        .Set("max_completion_tokens", (await action.Limit.Value()).Token)   // opened here: only the token goes
        .Set("tools", action.Tool);

    var send = new request(action.Context)                           // http, as today (OpenAi.cs:227–239)
    {
        Url = openai.Endpoint,
        Method = POST,
        Body = body,                                                 // written through its content type, json
        Header = new dict.@this().Set("Authorization", $"Bearer {openai.Key}"),
    };
    var reply = await new action.@this(send, action.Context).Start(action.Context);
    return reply.Success ? await answer.@this.Read(reply, conversation) : reply;   // open: Read
}
```

```csharp
// llm/openai/setting/this.cs: NEW
public sealed class @this : setting.module.@this
{
    [Out, Store, Sensitive, Ask("This program uses an LLM and needs a key. Paste it:")]
    public text.@this Key { get; set; } = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
    [Out, Store] public text.@this Endpoint { get; set; } = …;
    [Out, Store] public text.@this Model { get; set; } = "gpt-5.4-nano";
}

// actor/setting/this.cs: NEW, replaces Of<T>() (:310). The one door to a setting: read on use, asks, answers.
public async ValueTask<data.@this<T>> Get<T>() where T : type.item.setting.@this, new()
{
    // reads this setting's row by its path the first time it is asked for (no read at app start)
    // builds the class from its defaults ← the saved row ← this run's values, as Build does today (:290–306)
    // an empty option marked [Ask] is asked for once, through the actor's ask (secret when [Sensitive]), and saved
    // a row that can't be read, or an [Ask] nobody answered: the answer's error, never a throw
}
```

The body is the coder's. Its parts already exist: `Build` (`:290–306`), the lazy rows (`:76`), the ask and save in today's `Asked` (`llm/setting/this.cs:24–41`).

```csharp
// llm/openai/this.cs: OpenAI's reply read into an answer, or why there is none (856)
private async Task<data.@this<answer.@this>> Read(data.@this reply, conversation.@this conversation)
{
    var context = reply.Context;
    if (!(await reply.Get("choices[0]")).IsInitialized)
        return context.Error(new ActionError("No choices in LLM response", "EmptyResponse", 500));

    // OpenAI's words for why it stopped; the ones that mean "not a whole answer" are the answer's error
    if (Stopped[(await reply.Get("choices[0].finish_reason")).ToString()] is { } why)
        return context.Error(new ActionError(why.Message, why.Key, 400));

    return context.Ok(new answer.@this(
        await reply.Get("choices[0].message.content"),
        await reply.Get("choices[0].message.tool_calls"),
        new usage.@this(await reply.Get("usage")),
        conversation));
}

// OpenAI's stop words that aren't a whole answer (today's switch, OpenAi.cs:303–313)
//   length         → ResponseTruncated: the answer hit the token limit; raise limit.token or shorten the prompt
//   content_filter → ResponseFiltered:  the model refused, by its content filter
```

```csharp
// llm/type/answer/this.cs: NEW. One reply.
public sealed class @this : item.@this
{
    [Out, Store] public data.@this Content { get; }              // a carrier, read when used
    [Out, Store] public list.@this<ToolCall> Tool { get; }
    [Out, Store] public usage.@this Usage { get; }
    public conversation.@this Conversation { get; }              // what it answers
}

// llm/type/usage/this.cs: NEW. Tokens and cost, today 8 loose counters in Query (OpenAi.cs:193–200)
public sealed class @this : item.@this
{
    [Out, Store] public number.@this Prompt { get; }
    [Out, Store] public number.@this Completion { get; }
    [Out, Store] public number.@this Cached { get; }
    [Out, Store] public number.@this? Cost { get; }              // from the model's price, a setting per model
}
```

```csharp
// llm/openai/this.cs: in Ask, when the step streams (855)
if (action.OnStream is { } onStream)
{
    body.Set("stream", true)
        .Set("stream_options", new dict.@this().Set("include_usage", true));   // usage comes in the last event
    var reply = await /* the http request, its reply handed back as a stream to read (the http module's change) */;
    if (!reply.Success) return reply;
    return await answer.@this.Stream(reply, onStream, conversation);
    // NEW: reads OpenAI's events one by one; calls OnStream with each piece of text as %chunk%;
    // builds the whole answer (content, tool calls, usage) from them, the same answer as without streaming
}
```

> **For the coder: you own this code.** These are sketches of intent, agreed with Ingi line by line. Where the real code says another shape is better, bring it to the architect; don't build around it.

## Open: still to design with Ingi

- **Where `Ask` lives and how a provider plugs in, decided (859).** The provider is `ILlm`, the `[Code]` slot, picked by `code.setDefault`; `llm/openai/this.cs` implements its `Ask`. The loop is `conversation.Run`.
- **gpt-4 versus gpt-5+, decided (860).** Both are supported (Ingi: "we need to support both strcutures, your suggestion is good"). The difference is the model's, not OpenAI's wire: the gpt-5 and reasoning models refuse a non-default `temperature`/`top_p` and take reasoning settings. So the model is its own small type in the provider, one file per variant (`llm/openai/model/this.cs`, `gpt4.cs`, `gpt5.cs`):
  - each model has its name and its price (856), and writes its own part of the body: `model.Write(body, action)`;
  - gpt4 writes temperature and top_p, gpt5 its reasoning settings;
  - the provider writes the common part;
  - the model is found by its name in OpenAI's setting, the list that holds the prices, never by an `if` on the name.
- **Reading the reply, decided (856).** It's the provider's `Read(reply, conversation)`, beside `Ask`:
  - it reads through data's own navigation door (`reply.Get("choices[0].message.content")`, `data/this.Navigation.cs:14`), so `Nav<T>` goes;
  - no choices is `EmptyResponse`;
  - OpenAI's stop words that aren't a whole answer are one lookup in the provider (`length` → `ResponseTruncated`, `content_filter` → `ResponseFiltered`), not today's switch (`:303–313`);
  - it answers `answer` (NEW: `Content` a carrier, `Tool`, `Usage`, `Conversation`);
  - `usage` (NEW: `Prompt`, `Completion`, `Cached`, `Cost`) replaces the 8 loose counters (`:193–200`);
  - the conversation adds up each answer's usage across tool rounds;
  - prices become a setting per model, not the static table (`:41`).
- **Tools, decided (857).** A tool is a goal or a module action (Ingi: "we want to allow goals and also module.action to be called"): `tools: ReadReport, file.read` → `Tool = [ReadReport, file.read]`. Two variants in the llm module, so nothing is added to the core action (Ingi: "not sold on Offer"):
  - `tool/action.cs` holds the action (Ingi: "name it action") and answers from its catalog: its `.description.md` (`action/this.Schema.cs:106`) and its `Property` list. It's called with the model's arguments as the action's properties, checked by its own Parse and Validate (`action/this.Build.cs:42–50`), then Start.
  - `tool/goal.cs` holds the goal and answers from it: its `Comment` (`goal/this.cs:37`) and its declared parameters. It's called through goal.call, as today.

  What a value is (a goal or an action) names its variant, so nothing asks.

  The plang name stays `file.read`, held as two members, `Module` and `Action` (Ingi: "separate it into two properties"). OpenAI forbids a dot in a function name, so on its wire the provider writes `file_read` (Ingi: "lets use _, keep the names file.read") and finds a call by looking up the name it wrote, never by splitting it.

  `tool.list` is the `Tool` slot's own type: it runs the model's calls in order within the limit, a Parallel one running on (`:350–373`), each result a tool message in the conversation. The tool's `Description` is the goal's or the action's (today always "", `:186`). **The parameter schema, decided (863):** each type answers its own JSON schema (Ingi: "each type answer by it self, but if branch A is solving that, then great"). That's branch A's schema writer (813), and since llm-provider is built after branch A, `tool` uses it directly. `MapPlangTypeToJsonSchema` (`:795–816`, a switch on type names) goes with no stopgap. The answer's `Schema` gets the same: a type's own schema, from branch A. Safety: only what the step lists; running as the Service actor comes with the service-actor plan.
- **The answer's type and the check, decided (858).**
  - **`Format` becomes `Type`** (Ingi: "yes, seem right"), as `file.read`'s slot (814): "answer as json" and "as a list of names" are one slot, and the content is born as that type (`answer.Value(type)`). This is plang-visible: the builder's teaching for `llm.query` moves, under the teaching-edit rule.
  - **The instruction to the model** follows the asked type, as today (`BuildFormatInstruction`, `:709–722`): "You MUST respond in JSON" for json, "in ```md``` code block" for md, and so on (Ingi confirmed it). The conversation writes it into the system message when it's written out, never stored, so it doesn't pile up across rounds.
  - **A fenced block is taken out** when a type was asked for (`ExtractResponse`, `:726–743`).
  - **`answer.Check(validate, limit, conversation)`** runs `OnValidateResponse` with `%response%` in its own frame. A failure within `limit.retry` adds "Your response failed validation: …" and asks again; over the limit it answers `ValidationFailed`.
  - **The "Validation failed (retry n/m)" line** stays on the actor's output, as today (Ingi: "if validation failed it is an error, somebody needs to catch it … agent will be running as service actor, so the error is his not the user, so not sold on" moving it to debug). Revisit with the Service actor.
  - **The `Schema` slot** stays a hint in the instruction, as today. A type answering with its own schema is branch A's (813).
- **The answer holds its conversation and usage** as real members, not 15 `SetProp` lines (`:489–503`); decided in 856.
- **How a message looks in the provider's JSON, decided (862, Ingi: "yes, each should provider should know how to format it self").** The provider maps each message into its own shape, in `Ask`:
  - `await foreach (var message in conversation)`: the conversation (NEW: async enumerable) reads its carriers as it's walked;
  - `Message(message)` writes `{role, content, tool_call_id, tool_calls}` in OpenAI's words;
  - a fact the message doesn't have isn't written, so there's no branch on its role (today's four branches in `ToApiMessages`, `:585–654`, go);
  - the message's own names are plang's: `Role`, `Content`, `Answers` (today `ToolCallId`), `Call` (today `ToolCalls`);
  - images by name build on this in llm-writer's #3: `content` becomes the text, then a `%image%:` label and the image part for each image;
  - `ResolveImage`'s guessing (`:656`) moves to the image there.
- **Streaming, decided (855).** Today the step's `OnStream` gets OpenAI's raw event JSON as `%chunk%`, and the query then leaves the loop answering `Truncated` (`OpenAi.cs:216`, `:235–245`, `:508–518`). It becomes:
  - `%chunk%` is the next piece of text;
  - the step gets the same whole answer at the end;
  - the same loop runs (tools, check).

  The provider reads its own events: http hands back the reply's body as a stream to read (the http module's change), and `answer.Stream(reply, onStream, conversation)` (NEW) builds the answer from the events. The body gets `stream: true` and `stream_options: {include_usage: true}`. `%chunk%` changing from OpenAI's JSON to text is plang-visible, and Ingi said yes.
- **The llm's own cache: removed (861, Ingi: "ok, lets remove it").** It saves a request to the llm when the same hash comes again; it's not the step cache (`on.cache`, a step's result, in memory).
  - For the builder it saves nothing: the build asks once per goal, a changed goal is a new hash, and an unchanged goal never asks.
  - It's barely used: three builder steps and six test goals call `llm.query`, no app on this machine does, and no store holds an `LlmCache` table.
  - It was the envelope specimen Ingi kept (2026-09-28), which ends with it.
- **The model's fallback, decided (864, Ingi: "ok").**
  - **Which provider runs is set by the code generator:** a `[Code]` slot's `Attach` asks `app.Code.Get<ILlm>()` (`PLang.Generators/Emission/Property/Code/this.cs:33–40`), the registry `code.setDefault` changes. OpenAI is the default for now; plang's own provider later (Ingi).
  - **The default model belongs to the provider:** `Ask` writes `.Set("model", action.Model ?? openai.Model)`, the step's model when it names one, else the provider's own setting's (`%!llm.openai.setting.model%`).
  - `llm.query`'s `Model` slot has no default. The `??` asks only whether the step gave one.

  The timeout (2 minutes, hard-coded at `:234`) becomes OpenAI's setting `Timeout`. Ingi liked both (855). The request isn't marked `Unsigned` (Ingi: "dont have the request unsigned"); it changes nothing for OpenAI, since a request is never signed and `Unsigned` only refuses a signed plang reply (`http/request.cs:11`, `:41–42`).

## Still open: a model picks its provider (866, Ingi: "lets leave this for now")

Ingi: "when I do `- system: do stuff, model: "claude-5.5"`, how will it figure out it's the claude provider?" With the design above it wouldn't: the provider comes from `code.setDefault`, so the request would go to OpenAI and be refused. A sketch, not agreed:

- the `Model` slot holds a `model` (born by its name from the providers' models), not a text;
- each model knows its `Provider` and writes its own part of the body (860);
- each provider registers its models (OpenAI from its setting's model list, the one with the prices);
- `conversation.Run` asks `(await action.Model?.Value())?.Provider ?? action.Llm`;
- a name no provider serves fails at birth ("no provider serves the model claude-5.5"), and the build can warn early.

This would revise 864: the provider is the named model's, else `code.setDefault`'s.

## Stages (a sketch, fixed when the design is)

0. **Trace.** Every job in `OpenAi.Query` and the 16 helpers: whose job it is, and where it goes.
1. **Settings read on use, through one door.** `await context.Setting.Get<T>()` answers `data<T>`; no read at app start; `[Ask]`; `Of<T>()` gone. Then the provider folder, `llm/openai/`, with its own setting; `llm.setting.Asked` gone.
2. **The conversation stays plang to the end.** No `.Clr<List<LlmMessage>>()`; carriers read when written.
3. **`conversation.Run` and the provider's `Ask`.** The loop and the wire split; the answer read.
4. **Tools and the check** move to their owners.
5. **The rest of the demolition list.**

## Demolition (from what's traced so far)

- [ ] `OpenAi.Query`'s 475 lines, replaced by `conversation.Run` and the provider's `Ask`; `ILlm.Query` becomes `ILlm.Ask`
- [ ] `.Clr<List<LlmMessage>>()` (`OpenAi.cs:85`), and the `history.Clr<List<LlmMessage>>()` of the continue (`:139`)
- [ ] `CloneMessages` (`:1009`) and the `originalMessages` copy (`:145`)
- [ ] `ToApiMessages` (`:585`) and the C# dictionaries for the body (`:205–220`)
- [ ] `Content.ToString()` reads (`:595–647`)
- [ ] the 15 `SetProp` lines (`:489–503`, `:509–517`)
- [ ] `conversation.Continue` and its `Follow` (`:131–141`)
- [ ] `llm.setting.Asked` (`llm/setting/this.cs:21–41`); the key, endpoint and model leave the shared llm setting
- [ ] `Setting.Of<T>()` (`actor/setting/this.cs:310`): its 29 callers move to `await …Get<T>()`
- [ ] reading every saved setting at app start (`app/this.cs:405–406`, `Setting.Load()` at `actor/setting/this.cs:79–94`): a setting is read when it's first used
- [ ] the two throws for a bad row or a failed apply (`actor/setting/this.cs:295`, `:304`): `Get<T>` answers them
- [ ] the decider's own copy of a key (`llm/decider.setting.cs:19`), if the decider uses the provider's
- [ ] the llm's own cache (861):
  - the `Cache` slot on `llm.query` (`query.cs:92–95`) and the `Cache` setting (`llm/setting/this.cs:13`);
  - the `LlmCache` table with its key, check, store and restore (`OpenAi.cs:37`, `:155–174`, `:459–486`, `ComputeCacheKey`, `RestoreFromCache`);
  - `QueryCacheTests` (8);
  - `cache=%!build.setting.cache%` on the builder's three llm steps (`BuildGoal/Properties.goal:16`, `Start.goal:48`, `Start.goal:81`), rebuilt by the builder, never a hand-edited `.pr`. `%!build.setting.cache%` itself stays (the goal-level build cache).
- [ ] the builder's `Conversation={continue: %answer%}` (`BuildGoal/Start.goal:48`) becomes `Conversation=%answer%` (rule 5)

**Stays:**

- `query`'s slots and its one-line `Start`;
- `ILlm`;
- http's `request`;
- the message type;
- every llm test.

## What tests can't show (checks the coder runs and reports)

- **Every llm test stays green:** query, tools, conversation, cache, image, about 80 of them. Run them on recorded answers where they can be.
- **The builder's prompts are byte-equal** (the decider pins).
- **The locked surfaces are re-pinned** under 852: the setting type, `[Ask]`.
- **Every stage:** PLNG001/002 at 0, and no new red against runtime2.

## OBP validation (so far)

| new surface | check | result |
|---|---|---|
| `conversation.Run` | holds the loop's order only; no provider, no format, no opened slot it doesn't decide on | agreed (859) |
| `ILlm` | the provider: `Ask` only, picked by plang's existing `[Code]` door | agreed (859) |
| `conversation` | its own messages; carriers read when written; never the query | agreed |
| `Ask` | the provider's; takes the action and the conversation; answers data | agreed; where it lives is open |
| the provider setting | the provider owns its key, endpoint and model | agreed |
| `[Ask]` | one place, the setting's door, for every setting | agreed (locked, Ingi's yes) |
| `answer` | one reply; its own members, no property bag | open |
