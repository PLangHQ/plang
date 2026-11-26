# Folder structure

The folders the plang runtime itself reads and writes in an app. How to lay out your own goal files is a pattern of its own:
[App folders](./patterns/AppFolders.md).

## Root folder

The folder that holds `Start.goal` is the app's root. If you create the app in `C:\apps\TestApp`, then `TestApp` is the root.
`plang` run in that folder runs `Start.goal`; `plang build` builds every `.goal` file under it.

## Setup

`Setup.goal` in the root, or any `.goal` file in a `Setup/` folder, is a setup file: the schema of a database. Setup files run
when the app starts, each step once. See [Setup and migrations](./SetupAndMigrations.md).

## events

`events/Events.goal` holds the app's event bindings, and `events/BuilderEvents.goal` the bindings for the builder. Those two
file names are what the runtime looks for. The goals a binding calls can live in any folder. See [Events](./Events.md).

## .build

Written by `plang build`: one folder per goal file, with a `.pr` file per goal and per step. It is what the runtime runs. Do
not edit or delete it; build again instead.

## .db

The app's databases:

- `system.sqlite`: the runtime's own data, such as settings, the setup steps that have run and scheduled jobs.
- `data/data.sqlite`: the app's main database, created when a setup file creates a table.
- one folder per other datasource, for example `users/5/data.sqlite` for a [database per user](./patterns/PerEntityDatabase.md).

## .modules

`.dll` files that add modules to plang. See the [modules documentation](./modules/README.md).

## .services

`.dll` files that replace a part of plang, such as the llm service, caching or the database. See [Services](./Services.md).

## Your goal files

Everything else is yours. Folders are named for what they do (`user/`, `product/`, `order/`, `admin/`), and a route and the
goal it calls live in the folder of their thing; there is no `api/` folder. `ui/` holds the layout and templates and `deploy/`
the deploy goals. Because an event binds to a folder, the layout carries the permissions: [App folders](./patterns/AppFolders.md).
