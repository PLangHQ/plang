# PLang Object-Based Programming Philosophy

## Summary for Continuation

This document summarizes an architectural discussion about PLang's design philosophy, specifically moving toward **object-based programming** rather than variable/parameter-based programming.

---

## Core Idea

PLang should be designed around **passing objects directly** between operations, with C# modules handling the complexity internally. This contrasts with the traditional approach of having many specialized methods with many parameters.

### The Simplicity Principle

**Instead of this (current pattern):**
```csharp
// Complex module with many methods and parameters
public Task SetString(string name, string value)
public Task SetInt(string name, int value)
public Task SetList(string name, List<object> value)
public Task InsertIntoTable(string table, string col1, string col2, string col3, ...)
public Task HttpPost(string url, string body, Dictionary<string,string> headers, int timeout, string contentType, ...)
```

**Just this:**
```csharp
public Task Set(string name, object value, Type? type = null)
{
    memoryStack[name] = value;
}
```

One method. The object is the object.

---

## Applied to Modules

### Variable Module
```plang
- set %user% = %data%
```
C#: `Set(name, object)` - adds to `Dict<string, object>` on memoryStack. Done.

### HTTP Module
```plang
- get http://api.com/users, %filters%, write to %users%
- post http://api.com/order, %order%, write to %result%
```
C#: Method receives URL + object, internally creates HttpRequest, figures out format (JSON, form, query params), sends it, parses response.

### Database Module
```plang
- insert into user %data%, write to %user%
```
- Identity column handled automatically (no need to specify)
- Object goes in, object comes out
- C# figures out column mapping from object properties

### Email, File, etc.
```plang
- send email to %recipient%, %emailData%
- write to file %path%, %content%
```
Same pattern: target + object. Module handles details.

---

## Key Benefits

1. **LLM mapping becomes trivial** - only needs to identify: action, target, object, output variable
2. **No parameter explosion** - methods have 2-4 parameters max
3. **Intelligence lives in C#** - modules know HTTP conventions, DB patterns, serialization
4. **PLang stays natural** - closer to how humans describe tasks
5. **Less C# code** - fewer methods, fewer bugs, easier maintenance

---

## The Philosophy

1. **Objects are first-class** - pass them, store them, retrieve them whole
2. **No transformation at module boundary** - module doesn't need to understand object internals
3. **Objects flow through the system** - not decomposed into variables until absolutely necessary
4. **Defer property access** - let the endpoint (template, API, DB) access properties at point of use

---

## Optional Explicit Control

When fine control is needed, use optional hints (not required parameters):
```plang
- post http://..., %data%, content-type=xml, timeout=30sec
```

Hints are optional. The module has sensible defaults.

---

## Questions to Explore

1. How does this affect the Builder/LLM instruction generation?
2. What about error handling when C# must "figure things out"?
3. How to handle edge cases where explicit control IS needed?
4. Schema/type validation - where does it happen?
5. How does this philosophy apply to events, conditions, loops?

---

## Context: PLang Architecture

PLang compiles `.goal` files into `.pr` (JSON instructions) using LLM, then executes via C# runtime with reflection-based module invocation.

Current flow:
```
.goal → LLM Builder → .pr file → Runtime Engine → Module.Method(many params)
```

Proposed flow:
```
.goal → LLM Builder → .pr file → Runtime Engine → Module.Method(object)
```

The LLM's job becomes simpler. The C# module's job becomes smarter.
