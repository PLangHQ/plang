# PLang Actors & Messaging Architecture - Claude Code Implementation Plan

## Overview

This plan synthesizes two key architecture conversations:

1. **Actors & Channels** - Actor hierarchy, channel-based routing, content types, goal-scoped settings
2. **Messaging System** - Message types, sinks, transformers, envelope format

---

## Part 1: Actors Architecture

### 1.1 Actor Types

```csharp
public enum ActorType { System, User, Service }
```

| Actor | Purpose | ContentType Default | Singleton? |
|-------|---------|---------------------|------------|
| **System** | Runtime/host (logs, errors, lifecycle) | `text/plain` | Yes |
| **User** | External entity the app responds to (they came to you) | `plang/ndjson` | Per-connection |
| **Service** | External entities app reaches out to (you went to them) | `application/json` | Per-connection |

### 1.2 Actor Base Class

```csharp
public abstract class Actor
{
    public ActorType Type { get; }
    public IIdentity Identity { get; }
    public bool IsTrusted { get; }
    
    // Changeable via PLang
    public string ContentType { get; set; }
    public Encoding Encoding { get; set; }
    
    // Channels with sinks
    private readonly Dictionary<string, ActorChannel> _channels = new();
    
    protected Actor(ActorType type, IIdentity identity, bool isTrusted = false)
    {
        Type = type;
        Identity = identity;
        IsTrusted = isTrusted;
    }
    
    public ActorChannel GetChannel(string name) 
        => _channels.TryGetValue(name, out var ch) ? ch : null;
    
    public ActorChannel GetOrCreateChannel(string name)
    {
        if (!_channels.TryGetValue(name, out var channel))
        {
            channel = new ActorChannel(name, CreateDefaultSink());
            _channels[name] = channel;
        }
        return channel;
    }
    
    public void RegisterChannel(string name, IOutputSink sink, string? contentType = null)
        => _channels[name] = new ActorChannel(name, sink, contentType);
    
    public void UnregisterChannel(string name) => _channels.Remove(name);
    
    protected abstract IOutputSink CreateDefaultSink();
    
    public string GetContentType(string channel)
        => GetChannel(channel)?.ContentType ?? ContentType;
}
```

### 1.3 Concrete Actor Implementations

```csharp
public class SystemActor : Actor
{
    public SystemActor(IIdentity appIdentity) 
        : base(ActorType.System, appIdentity, isTrusted: true)
    {
        ContentType = "text/plain";
    }
    
    protected override IOutputSink CreateDefaultSink() => new ConsoleSink();
}

public class UserActor : Actor
{
    public UserActor(IIdentity identity, bool isTrusted = false) 
        : base(ActorType.User, identity, isTrusted)
    {
        ContentType = "plang/ndjson";
    }
    
    protected override IOutputSink CreateDefaultSink()
    {
        throw new InvalidOperationException("User channels must be explicitly registered");
    }
}

public class ServiceActor : Actor
{
    public string Endpoint { get; }
    
    public ServiceActor(IIdentity identity, string endpoint) 
        : base(ActorType.Service, identity)
    {
        Endpoint = endpoint;
        ContentType = "application/json";
    }
    
    protected override IOutputSink CreateDefaultSink() => new ForwardToSystemSink();
}
```

### 1.4 Actor Channel

```csharp
public class ActorChannel
{
    public string Name { get; }
    public IOutputSink Sink { get; set; }
    public string? ContentType { get; set; }  // null = use actor's default
    public Encoding? Encoding { get; set; }
    
    // Goal-scoped overrides (keyed by Goal)
    private readonly Dictionary<Goal, ChannelSettings> _goalOverrides = new();
    
    public ActorChannel(string name, IOutputSink sink, string? contentType = null)
    {
        Name = name;
        Sink = sink;
        ContentType = contentType;
    }
    
    public void SetScopedSettings(Goal goal, ChannelSettings settings)
        => _goalOverrides[goal] = settings;
    
    public void ClearScopedSettings(Goal goal)
        => _goalOverrides.Remove(goal);
    
    // Resolve by walking up call stack
    public string GetEffectiveContentType(CallStack callStack, string actorDefault)
    {
        foreach (var goal in callStack.FromTopToBottom())
        {
            if (_goalOverrides.TryGetValue(goal, out var settings) && settings.ContentType != null)
                return settings.ContentType;
        }
        return ContentType ?? actorDefault;
    }
}

public class ChannelSettings
{
    public string? ContentType { get; set; }
    public Encoding? Encoding { get; set; }
}
```

---

## Part 2: Context Architecture

### 2.1 Two Contexts

| Context | Lifetime | Purpose |
|---------|----------|---------|
| **AppContext** | App lifetime | Trusted system and user (console/desktop user) |
| **PLangContext** | Per-request | Request's user and any outbound service connections |

### 2.2 AppContext

```csharp
public class AppContext
{
    public SystemActor System { get; }
    public UserActor User { get; }  // Trusted user (server operator, desktop user)
    
    public AppContext(IIdentity appIdentity)
    {
        System = new SystemActor(appIdentity);
        User = new UserActor(appIdentity, isTrusted: true);
        
        // Both default to console for console/desktop apps
        System.RegisterChannel("default", new ConsoleSink());
        User.RegisterChannel("default", new ConsoleSink());
    }
}
```

### 2.3 PLangContext

```csharp
public class PLangContext
{
    public Actor System { get; set; }  // Points to actor that handles system output
    public Actor User { get; set; }    // Current user for this execution
    public Actor? Service { get; set; } // Outbound connection if any
    
    // For console/desktop: mirrors AppContext
    public static PLangContext FromAppContext(AppContext app)
    {
        return new PLangContext
        {
            System = app.System,
            User = app.User
        };
    }
    
    // For web request: User is untrusted, System escalates to AppContext.User
    public static PLangContext ForWebRequest(AppContext app, IIdentity requestIdentity, IOutputSink httpSink)
    {
        var user = new UserActor(requestIdentity, isTrusted: false);
        user.RegisterChannel("default", httpSink);
        
        return new PLangContext
        {
            System = app.User,  // Escalate: system output goes to server operator
            User = user
        };
    }
}
```

### 2.4 Engine Flow

```
plang.exe starts
    → new Engine()
    → engine.AppContext = new AppContext(appIdentity)
    → engine.PLangContext = PLangContext.FromAppContext(appContext)

web request arrives
    → engine = getEngineFromPool()
    → engine.PLangContext = PLangContext.ForWebRequest(appContext, requestIdentity, httpSink)
```

---

## Part 3: Messaging System

### 3.1 Message Kinds

```csharp
public enum MessageKind { Text, Render, Execute, Ask, Stream }
```

### 3.2 Base Message

```csharp
public abstract record OutMessage(
    MessageKind Kind,
    string Content,
    string Actor = "user",
    string? Channel = null,
    string? Target = null,
    string? Action = null,
    string Level = "info",
    int StatusCode = 200,
    bool Terminate = false,
    IReadOnlyDictionary<string, object?>? Meta = null
);
```

### 3.3 Concrete Messages

```csharp
public sealed record TextMessage(
    string Content,
    string Actor = "user",
    string? Channel = null,
    string Level = "info",
    int StatusCode = 200,
    IReadOnlyDictionary<string, object?>? Meta = null
) : OutMessage(MessageKind.Text, Content, Actor, Channel, null, "append", Level, StatusCode, false, Meta);

public sealed record RenderMessage(
    string Content,
    string? Target = null,
    string Action = "replace",
    string Actor = "user",
    string? Channel = null,
    IReadOnlyDictionary<string, object?>? Meta = null
) : OutMessage(MessageKind.Render, Content, Actor, Channel, Target, Action, "info", 200, false, Meta);

public sealed record ErrorMessage(
    string Content,
    int StatusCode = 500,
    bool Terminate = true,
    string Actor = "user",
    string? Channel = null,
    IReadOnlyDictionary<string, object?>? Meta = null
) : OutMessage(MessageKind.Text, Content, Actor, Channel, null, null, "error", StatusCode, Terminate, Meta);

public sealed record AskMessage(
    string Content,
    string? Variable = null,
    string Actor = "user",
    string? Channel = null,
    IReadOnlyDictionary<string, object?>? Meta = null
) : OutMessage(MessageKind.Ask, Content, Actor, Channel, null, "ask", "info", 200, false, Meta);

public sealed record ExecuteMessage(
    string Function,
    object? Data = null,
    string Actor = "user",
    string? Channel = null,
    IReadOnlyDictionary<string, object?>? Meta = null
) : OutMessage(MessageKind.Execute, Function, Actor, Channel, null, null, "info", 200, false, Meta);

public enum StreamPhase { Start, Chunk, End, Abort }

public sealed record StreamMessage(
    string StreamId,
    StreamPhase Phase,
    string? Text = null,
    ReadOnlyMemory<byte>? Bytes = null,
    string ContentType = "application/octet-stream",
    string Actor = "user",
    string? Channel = null,
    IReadOnlyDictionary<string, object?>? Meta = null
) : OutMessage(MessageKind.Stream, StreamId, Actor, Channel, null, "stream", "info", 200, false, Meta)
{
    public bool HasBinary => Bytes.HasValue && !Bytes.Value.IsEmpty;
    public bool HasText => !string.IsNullOrEmpty(Text);
}
```

---

## Part 4: Sinks & Transformers

### 4.1 Sink Interface

```csharp
public interface IOutputSink
{
    Task<IError?> SendAsync(OutMessage message, CancellationToken ct = default);
}
```

### 4.2 Transformer Selection

Content type prefix determines envelope vs raw:

| ContentType | Output Format |
|-------------|---------------|
| `plang/json` | Full OutMessage envelope as JSON (will be signed) |
| `plang/msgpack` | Full OutMessage envelope as msgpack (will be signed) |
| `plang/ndjson` | Full OutMessage envelope as JSON lines (will be signed) |
| `application/json` | Just the content/data as JSON |
| `application/msgpack` | Just the content/data as msgpack |
| `text/plain` | Just the content as text |

```csharp
public static class TransformerFactory
{
    public static ITransformer Create(string contentType, Encoding encoding)
    {
        if (contentType.StartsWith("plang/"))
        {
            var format = contentType.Replace("plang/", "");
            return CreateEnvelopeTransformer(format, encoding);
        }
        return CreateRawTransformer(contentType, encoding);
    }
    
    private static ITransformer CreateEnvelopeTransformer(string format, Encoding encoding)
    {
        return format switch
        {
            "json" => new PlangJsonTransformer(encoding),
            "ndjson" => new PlangNdjsonTransformer(encoding),
            "msgpack" => new PlangMsgPackTransformer(encoding),
            _ => new PlangJsonTransformer(encoding)
        };
    }
    
    private static ITransformer CreateRawTransformer(string contentType, Encoding encoding)
    {
        return contentType switch
        {
            "application/json" => new JsonTransformer(encoding),
            "application/msgpack" => new MsgPackTransformer(encoding),
            "text/html" => new HtmlTransformer(encoding),
            _ => new TextTransformer(encoding)
        };
    }
}
```

### 4.3 ConsoleSink

```csharp
public class ConsoleSink : IOutputSink
{
    public Task<IError?> SendAsync(OutMessage message, CancellationToken ct = default)
    {
        var output = FormatOutput(message);
        if (output == null) return Task.FromResult<IError?>(null);
        
        var color = GetColor(message);
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(output);
        Console.ForegroundColor = originalColor;
        
        return Task.FromResult<IError?>(null);
    }
    
    private string? FormatOutput(OutMessage message)
    {
        var content = GetContent(message);
        if (content == null) return null;
        
        var isDefaultChannel = string.IsNullOrEmpty(message.Channel) ||
                               message.Channel.Equals("default", StringComparison.OrdinalIgnoreCase);
        
        if (isDefaultChannel) return content;
        
        return $"{BuildPrefix(message)}{content}";
    }
    
    private string BuildPrefix(OutMessage message)
    {
        var parts = new List<string>();
        
        if (!string.IsNullOrEmpty(message.Channel))
            parts.Add($"[{message.Channel}]");
        
        if (!message.Level.Equals("info", StringComparison.OrdinalIgnoreCase))
            parts.Add($"[{message.Level.ToUpperInvariant()}]");
        
        if (message.StatusCode != 200)
            parts.Add($"[{message.StatusCode}]");
        
        if (!message.Actor.Equals("user", StringComparison.OrdinalIgnoreCase))
            parts.Add($"[{message.Actor}]");
        
        return parts.Count == 0 ? "" : string.Join(" ", parts) + " ";
    }
    
    private ConsoleColor GetColor(OutMessage message)
    {
        return message.Level.ToLowerInvariant() switch
        {
            "error" => ConsoleColor.Red,
            "warning" => ConsoleColor.Yellow,
            "debug" => ConsoleColor.Gray,
            _ => message.StatusCode >= 400 ? ConsoleColor.Red : ConsoleColor.White
        };
    }
}
```

---

## Part 5: PLang Integration

### 5.1 PLang Examples

```plang
- write out "hello"
/ writes to User actor, default channel

- write out "Debug info", to system
/ writes to System actor

- render product.html, target #main, append
/ RenderMessage to User

- ask "What is your name?", write to %name%
/ AskMessage to User

- throw error "name is missing"
/ ErrorMessage to User

- write to info log "access granted"
/ TextMessage to System, channel: "log"
```

### 5.2 Configuring Output

```plang
- configure output to "application/json"
/ context scope (default), lasts whole request

- configure output to "msgpack", scope: goal
/ goal scope, auto-clears on ExitGoal

- configure output to "plang/json", actor: system
/ changes System actor's ContentType

- configure output encoding to "utf-16"
/ changes User actor's Encoding
```

### 5.3 GetActor Method in ProgramBase

```csharp
public Actor GetActor(string actorName)
{
    var context = _contextAccessor.Current;
    
    return actorName?.ToLowerInvariant() switch
    {
        "system" => context.System,
        "service" => context.Service ?? context.System,  // fallback if no service
        _ => context.User  // default
    };
}
```

---

## Part 6: Goal-Scoped Settings Integration

### 6.1 ExitGoal Hook

```csharp
public void ExitGoal(Goal goal)
{
    // ... existing exit logic ...
    
    // Clear any scoped output settings for this goal
    var context = _contextAccessor.Current;
    
    foreach (var actor in new[] { context.System, context.User, context.Service })
    {
        if (actor == null) continue;
        foreach (var channel in actor.GetAllChannels())
        {
            channel.ClearScopedSettings(goal);
        }
    }
}
```

---

## Implementation Order for Claude Code

### Phase 1: Core Types
1. `ActorType` enum
2. `ActorChannel` class
3. `Actor` abstract class
4. `SystemActor`, `UserActor`, `ServiceActor` implementations

### Phase 2: Context
1. `AppContext` class
2. `PLangContext` class
3. Integration with Engine

### Phase 3: Messaging
1. `MessageKind` enum
2. `OutMessage` base record
3. Concrete message types (TextMessage, RenderMessage, ErrorMessage, AskMessage, ExecuteMessage, StreamMessage)

### Phase 4: Sinks
1. `IOutputSink` interface
2. `ConsoleSink` implementation
3. `ForwardToSystemSink` implementation

### Phase 5: Transformers
1. `ITransformer` interface
2. `TransformerFactory`
3. Envelope transformers (PlangJsonTransformer, etc.)
4. Raw transformers (JsonTransformer, TextTransformer, etc.)

### Phase 6: Integration
1. Wire up to Engine
2. Implement `GetActor()` in ProgramBase
3. Implement PLang syntax mapping for output commands
4. Goal-scoped settings with CallStack integration

---

## Files to Create/Modify

```
PLang/
├── Runtime/
│   ├── Actors/
│   │   ├── Actor.cs
│   │   ├── ActorType.cs
│   │   ├── ActorChannel.cs
│   │   ├── ChannelSettings.cs
│   │   ├── SystemActor.cs
│   │   ├── UserActor.cs
│   │   └── ServiceActor.cs
│   ├── Context/
│   │   ├── AppContext.cs
│   │   └── PLangContext.cs
│   ├── Messaging/
│   │   ├── MessageKind.cs
│   │   ├── OutMessage.cs
│   │   ├── TextMessage.cs
│   │   ├── RenderMessage.cs
│   │   ├── ErrorMessage.cs
│   │   ├── AskMessage.cs
│   │   ├── ExecuteMessage.cs
│   │   └── StreamMessage.cs
│   ├── Sinks/
│   │   ├── IOutputSink.cs
│   │   ├── ConsoleSink.cs
│   │   ├── HttpSink.cs
│   │   └── ForwardToSystemSink.cs
│   └── Transformers/
│       ├── ITransformer.cs
│       ├── TransformerFactory.cs
│       ├── PlangJsonTransformer.cs
│       ├── PlangNdjsonTransformer.cs
│       ├── JsonTransformer.cs
│       └── TextTransformer.cs
```