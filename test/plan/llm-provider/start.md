# llm-provider

**Status: design in progress.** This holds what Ingi and I have agreed so far; the open pieces are listed at the end. Nothing is built before the design is done and Ingi says go.

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
2. **One loop, no provider in it:** `llm/code/Default.cs`. Each provider is only its wire, in its own folder (Ingi: "it should be called llm/code/default.cs, and we then have the openai format … then others can plugin").
3. **A value formats itself** (Ingi: "who is responsible for formatting a value. Answer: the value is responsible. so why are seeing a variable called format there?"). `Default` holds no format.
4. **The conversation puts itself together** (Ingi: "why doen't conversation handle the sticting to gether the conversation"). It holds only its messages, never the query that made it (Ingi: "why is borned with query?").
5. **`Conversation` holds the earlier answer it continues:** `continue the conversation in %first%` builds `Conversation = %first%`. There's no `{continue}` record, no `Follow`, and no `"Messages"` property read by name. How the answer holds its conversation comes later (Ingi: "lets get back to this later").
6. **`Ask` takes the action and the conversation, and answers data:** an answer, or why there is none. There's no `request` type (Ingi: "why does Ask not just take in actoin and conversation?"; "what if there is an error, we dont handle errors?").
7. **The key, endpoint and model are the provider's own setting** (`llm/openai/setting/this.cs`, `%!llm.openai.setting.key%`). The shared llm setting keeps only what every provider shares (Ingi: "it should not be llm/settings/this.cs for openai, what happens for the next llm?").
8. **A setting asks for itself** (Ingi: "a setting should ask by it self if key is missing, it should not be done by the higher level class that uses it" → "yes better"). An option marked `[Ask("…")]` that is empty when read through the setting's door is asked for once and saved. This is a locked addition (the setting type and a new attribute) with Ingi's yes. `llm.setting.Asked` goes.

9. **A setting is read through one async door, and only when used** (Ingi, 854: "setting should always be await/async, but why is it .Of, why not .Get?"; "value should only be retrieve/load/materialized, on usage" → "yes"). `await context.Setting.Get<T>()` answers `data<T>`. On first use it reads that setting's row by its path, asks for an empty `[Ask]` option, and answers a bad row or a key nobody gave as an error, not a throw. App start reads no settings; the line at `app/this.cs:406` goes. `Of<T>()` (`actor/setting/this.cs:310`) goes, and its 29 callers move to `await …Get<T>()`. This is a locked addition (the settings' surface), with Ingi's yes.

## The design so far

```
llm/query.cs                  the action: its slots; Start => Llm.Query(this), one line (stays)
llm/code/ILlm.cs              the [Code] slot (stays)
llm/code/Default.cs           NEW: the loop
llm/type/conversation/        the conversation: the earlier answer's messages, the step's, what the rounds add
llm/type/answer/              NEW: one reply: content, why it stopped, tool calls, usage
llm/openai/this.cs            NEW: OpenAI's wire, its Ask
llm/openai/setting/this.cs    NEW: OpenAI's key, endpoint, model
llm/setting/this.cs           shared: length, rows, inline, cache, which provider
```

```csharp
// llm/code/Default.cs: NEW (it replaces OpenAi.Query)
public sealed class Default : ILlm
{
    public async Task<data.@this> Query(query action)
    {
        var conversation = new conversation.@this(action.Message, action.Conversation);
        while (true)
        {
            var asked = await Ask(action, conversation);        // the provider's wire (open: where Ask lives)
            if (!asked.Success) return asked;                   // the call failed, or the model stopped early
            var answer = await asked.Value();                   // opened here: the loop decides on it
            conversation.Add(answer.Message);

            if (answer.Tool.Count > 0)                          // open: tools
            {
                var called = await answer.Call(action.Tool, action.Limit, conversation);
                if (!called.Success) return called;
                continue;
            }

            var checked = await answer.Check(action.OnValidateResponse, action.Limit, conversation);   // open: the check
            if (!checked.Success) return checked;
            if (checked.Retry) continue;

            return asked;                                       // the answer, holding its conversation
        }
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

## Open: still to design with Ingi

- **Where `Ask` lives and how a provider plugs in.** The body has OpenAI's field names, so `Ask` is the provider's; `Default` must not hold it. One provider per folder, picked by a setting. How `Default` reaches the provider's `Ask` is open.
- **gpt-4 versus gpt-5+.** Their JSON differs (Ingi: "watch out for gpt4 and gp5+ json difference"). One format file each, picked by a setting, never by an `if` on the model name.
- **Reading the reply,** `answer.Read(reply, conversation)`. Today that's `OpenAi.cs:250–326`: the content, the stop reasons (`length`, `content_filter`) as the answer's failure, the tool calls, the usage.
- **Usage and cost.** The price table is static in OpenAi (`:41`); lean: a setting per model.
- **Tools:** the model's calls run against the step's tools, within the limit; each tool writes its own schema (today `BuildParamSchema` and `MapPlangTypeToJsonSchema`, `OpenAi.cs:762`, `:795`; todos.md 2026-09-23).
- **The check:** `OnValidateResponse` and the retry message (today `:417–449`).
- **The answer holds its conversation and usage** as real members, not 15 `SetProp` lines (`:489–503`).
- **How a message looks in the provider's JSON** (llm-writer's #3): an image by name, with a `%image%:` text part before each image part, since OpenAI's image part has no name field.
- **Streaming** (`OnStream`): today it leaves the loop at `:240–245`.
- **The llm's own cache:** the envelope specimen Ingi kept on purpose (2026-09-28, "this is obpv. I dont want to fix it now"; direction: `on.cache`). Keep it unchanged, or fix it now: Ingi's call.
- **The model's fallback** (an empty `Model` → the setting's, `:70`): the slot's own default, not an `if`.

## Stages (a sketch, fixed when the design is)

0. **Trace.** Every job in `OpenAi.Query` and the 16 helpers: whose job it is, and where it goes.
1. **Settings read on use, through one door.** `await context.Setting.Get<T>()` answers `data<T>`; no read at app start; `[Ask]`; `Of<T>()` gone. Then the provider folder, `llm/openai/`, with its own setting; `llm.setting.Asked` gone.
2. **The conversation stays plang to the end.** No `.Clr<List<LlmMessage>>()`; carriers read when written.
3. **`Default` and `Ask`.** The loop and the wire split; the answer read.
4. **Tools and the check** move to their owners.
5. **The rest of the demolition list.**

## Demolition (from what's traced so far)

- [ ] `OpenAi.Query`'s 475 lines, replaced by `Default.Query` and the provider's `Ask`
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
| `Default` | holds the loop's order only; no provider, no format, no opened slot it doesn't decide on | sketched |
| `conversation` | its own messages; carriers read when written; never the query | agreed |
| `Ask` | the provider's; takes the action and the conversation; answers data | agreed; where it lives is open |
| the provider setting | the provider owns its key, endpoint and model | agreed |
| `[Ask]` | one place, the setting's door, for every setting | agreed (locked, Ingi's yes) |
| `answer` | one reply; its own members, no property bag | open |
