# Conversation Log: Object-Based Programming Philosophy

**Date:** 2026-02-04
**Participants:** Ingi Gauti, Claude (Opus 4.5)
**Topic:** PLang architectural philosophy - object-based vs variable-based programming

---

## Context

Started with exploring PLang debugging system (Events.goal, SendDebug.goal, VS Code extension debugAdapter.ts). Then shifted to architectural discussion.

---

## Conversation

### User
> I want to discuss the architecture, and idea, object based programming, not variable based programming (you can change variables and there are variables, but you mostly work with objects, transfering them between one place to another).
>
> For example for db module, I have very complex functions and many, instead of mapping to plang language, pseudo code:
> - `insert into user %data%, %identity%` (þarf ekki að skrifa, gerist alltaf), `write to %user%`
> - `change table user, name=%data.name%` // þá alltaf þegar insert, þá insert þarna í name, þá er hægt að indexa betur fyrir WHERE

### Claude
Asked for clarification on the "change table" concept and whether user meant schema definition, index hints, or dynamic column selection.

### User
> No, those are examples, I am talking about an idea of this. Set how easy it would be if I had variable class, and it had only `setVariable(name, value:object, type?:null)`, and I'd just add it to `Dict<string, key>` on the engine called memoryStack, no other method.

### Claude
Understood the philosophy:

**The Simplicity Principle**

Instead of:
```csharp
// Complex module with many methods
public Task SetString(string name, string value)
public Task SetInt(string name, int value)
public Task SetList(string name, List<object> value)
public Task SetDictionary(string name, Dictionary<string, object> value)
public Task AppendToList(string name, object item)
public Task RemoveFromList(string name, int index)
// ... 20 more methods
```

Just:
```csharp
public Task Set(string name, object value, Type? type = null)
{
    memoryStack[name] = value;
}
```

That's it. One method. The object is the object.

**The Philosophy:**
1. Objects are first-class - pass them, store them, retrieve them whole
2. No transformation at the module boundary
3. The LLM's job becomes trivial
4. Less C# code = fewer bugs

**Applied broadly:**
- DbModule: `insert(table, object)` - not `insert(table, col1, col2, col3...)`
- HttpModule: `post(url, object)` - not decomposed headers, body, params
- FileModule: `write(path, object)` - serialize automatically

### User
> Yes, to everything. `get http://... %data%, write to %response%`, in the C# method create the HttpRequest and send the data, in the format that was requested.

### Claude
Confirmed understanding:

**The Pattern**

PLang syntax:
```plang
- get http://api.com/users, %filters%, write to %users%
- post http://api.com/order, %order%, write to %result%
- insert into orders %order%, write to %savedOrder%
- send email to %recipient%, %emailData%
```

C# does the work:
```csharp
public async Task<object?> Get(string url, object? data = null)
{
    var request = new HttpRequestMessage(HttpMethod.Get, url);

    if (data != null)
    {
        // Figure out: query params? headers? body?
        // Based on what makes sense for GET
    }

    var response = await client.SendAsync(request);
    return await ParseResponse(response); // JSON? XML? Auto-detect
}
```

**What This Means:**
1. LLM mapping becomes trivial - only identify: action, target, object, output variable
2. No parameter explosion - methods have 2-4 parameters max
3. Intelligence lives in C# - module knows HTTP conventions, DB patterns
4. PLang stays natural - closer to how humans think

**Trade-off:** Less explicit control from PLang. But optional hints available when needed:
```plang
- post http://..., %data%, content-type=xml, timeout=30sec
```

### User
> Yes, this is the idea. Now I want to take this talk to Claude web and discuss. Can you make me a document about our conversation to summarize so I can continue the conversation, and a file of our conversation.

---

## Key Insights

1. **Object-flow over variable manipulation** - Objects transfer between operations whole
2. **C# modules are smart, PLang syntax is simple** - Push complexity into implementation
3. **LLM Builder job simplified** - Just identify action + target + object + output
4. **Natural language stays natural** - Don't force users to think about parameters
5. **Optional hints for edge cases** - Sensible defaults, explicit control when needed

---

## Open Questions for Further Discussion

1. How does this affect the Builder/LLM instruction generation?
2. Error handling when C# must infer intent?
3. Type safety and validation - where does it happen?
4. Migration path from current parameter-heavy modules?
5. How to handle truly complex operations that need explicit control?
6. Does this philosophy extend to conditionals, loops, events?
7. Performance implications of dynamic object handling?
8. Debugging experience - easier or harder with implicit behavior?
