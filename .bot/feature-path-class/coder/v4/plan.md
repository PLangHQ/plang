# Plan: v4 — Address v3 review: Path stores Engine, all handlers become pure delegators

## Context

v3 review identified remaining OBP violations:
- **delete.cs**: IgnoreIfNotFound logic lives in handler, not Path
- **exists.cs**: creates @file directly instead of delegating to Path
- **save.cs**: passes `Context.Engine!` as param — Path should navigate to it internally
- **Tests**: System.IO.File usage forbidden — must use `_fs` (IPLangFileSystem)

Ingi's direction: "the action class method should just send the object and not all the parameters in the object"

## Key Design Decision: Path stores Engine

Path constructor takes `Engine.@this` instead of `IPLangFileSystem`. Extracts `_fs` from `engine.FileSystem`. Save uses `_engine.Channels.Serializers` internally.

## Changes made

1. Path.cs — Store Engine, Save drops param, Delete absorbs IgnoreIfNotFound, add AsFile()
2. save.cs — `Path.Save(Value)`
3. delete.cs — `Path.Delete(Recursive, IgnoreIfNotFound)`
4. exists.cs — `Path.AsFile()`
5. PathTests.cs — Engine field, System.IO → _fs, new tests
6. FileHandlerTests.cs — System.IO → _fs, constructors use _engine
