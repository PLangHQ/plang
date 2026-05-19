# Build Mode

PLang transforms `.goal` files (natural language) into `.pr` files (JSON) using an LLM. Build mode is activated via `plang build` or `--build`.

All build options are passed as JSON via `--build={...}`. The JSON properties map directly to `Builder.@this` class properties.

Code: `PLang/app/modules/builder/this.cs`

> **Rebuilding the builder itself?** Go straight to [building-the-builder.md](building-the-builder.md) — the bootstrap case has specific cwd and file-order requirements that nothing here covers.

## Usage

```bash
# Build all .goal files in current directory
plang build

# Build a specific file
plang build '--build={"files":"myfile.goal"}'

# Build multiple files
plang build '--build={"files":["file1.goal","file2.goal"]}'

# Build without LLM cache (forces fresh LLM call)
plang build '--build={"cache":false}'

# Combine options
plang build '--build={"files":"myfile.goal","cache":false}'
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `files` | string or string[] | (all) | File filter. Single string or array of filenames. Only matching .goal files are built. |
| `cache` | bool | true | Whether to use the LLM response cache. Set to false to force a fresh LLM call. |

## How It Works

1. `Build.goal` (system builder) is the entry point
2. For each `.goal` file, `BuildGoal.goal` sends the goal text + action summary to the LLM
3. The LLM returns `{module, action, parameters}` mappings for each step
4. The result is validated, merged, and saved as a `.pr` file

## Cache

LLM responses are cached in `.db/system.sqlite`. When building the same goal with the same content, the cached response is used (fast, ~0.4s). With `cache:false`, the cache is bypassed and the LLM is called fresh (~2-4s).

The cache key is based on the goal content hash. If you change the `.goal` file, the cache is automatically invalidated.

## Combining with Debug

```bash
# Build one file, debug the builder, watch variables
plang build '--build={"files":"myfile.goal","cache":false}' '--debug={"goal":"BuildGoal","variables":["%actionSummary%","%goalForLlm%"]}'
```

See [debug.md](debug.md) for full debug documentation.

## Builder Version

Each built `.pr` file includes a `version` field set by the builder. This tracks which builder version produced the file.
