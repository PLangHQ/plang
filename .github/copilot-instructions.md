# Plang Codebase Instructions for AI Agents

## Overview
Plang is a natural language programming language that uses LLMs to compile pseudo-code into executable C# instructions. Core units are `.goal` files (programs/scripts). Focus on SQLite databases, event-driven workflows, and AI-assisted parsing.

## Architecture
- **Goals**: `.goal` files contain natural language steps. Built via LLM into `.pr` files.
- **Modules**: Extensions in `system/modules/` (e.g., `DbModule` for CRUD).
- **Services**: Overrides in `system/services/` (e.g., variable management).
- **LLM Integration**: Prompts in `system/llm/` for parsing steps.
- **UI**: Server-side HTML rendering via `system/ui/`.
- **Events**: Bindings in `system/events/` for cross-cutting concerns.
- Data flows: Goals → LLM parsing → Runtime execution → Modules handle I/O → Events trigger.

## Critical Workflows
- **Build**: Run `plang build` (parses goals via LLM, validates). Errors: LLM retries invalid steps.
- **Run**: `plang run` (executes goals, handles caching/errors).
- **Test**: Write `.goal` tests with `expect` assertions. Run via `RunAllTests.goal`.
- **Publish**: `dotnet publish` for cross-platform binaries (see `Publish/Publish.goal`).
- **Debug**: Add `write out` for logging; LLM fixes build issues.

## Project-Specific Conventions
- **Variables**: Always use `%variableName%` (e.g., `%userId%`, `%Settings.ApiKey%`).
- **Goal Calls**: `call !GoalName %params%` (e.g., `call !ProcessUser %data%`).
- **Validation**: `make sure %var% is not empty` or `validate %email% contains @`.
- **Database**: SQLite-first. `insert into table, field=%val%` (auto-ID, event-sourced).
- **Authentication**: Use `%Identity%` (Ed25519-derived, passwordless).
- **UI Rendering**: `[ui] render "template.html"`.
- **Events**: Bind in `Events.goal` (e.g., `before each goal in /api/.*, call CheckAuth`).
- **LLM Calls**: `[llm] system: %prompt% user: %input% scheme: %jsonSchema% write to %result%`.
- **Naming**: CamelCase goals (e.g., `CreateUser`). Separate `Setup.goal` (init) from `Start.goal` (entry).
- **Structure**: Apps as folders with `api/`, `ui/`, `events/`. Avoid hard-coding; use settings.

## Examples
- **Goal**: `Start - start webserver on port 8080` (from `Start.goal`).
- **Step**: `- read file.txt to %content%` → Maps to `FileModule.ReadTextFile`.
- **Test**: `CreateDataSourceTest - call CreateDataSource name="test" - expect %count% == 1`.
- **Error**: `on error call HandleErrors handlers=%handlers%`.
- **DB**: `insert into users, %email%, %hash%` (event-sourced).

Reference: [Documentation/README.md](Documentation/README.md), [system/Build.goal](system/Build.goal), [Tests/](Tests/).