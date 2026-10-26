# Databases

For someone writing a program. The question this page answers: can my goal use a database?

This runtime has no database module. `PLang/app/module/` holds these modules: archive, assert, browser, build, cache, callback, channel, code, condition, crypto, debug, error, file, goal, http, identity, list, llm, loop, math, module, on, output, screen, setting, signing, snapshot, task, terminal, test, timer, ui, variable, window. There is no `db`. The folder `os/system/modules/db/` holds only `Builder/llm`, which belongs to the builder.

A program keeps data in files with the [file module](../os/system/modules/file/start.md), and keeps its own configuration with `save` on a setting (`os/system/modules/setting/save.description.md`).

PLang itself keeps an app's settings in a SQLite file, `.data/setting/data.sqlite`, in the app's folder. That file is PLang's own, for settings. Whether a goal can query it was not tried.

A step such as `select * from users` was not tried either. This page claims only that no database module exists, so there is no action written for it.
