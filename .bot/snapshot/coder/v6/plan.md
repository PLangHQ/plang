# v6 — the one write (snapshot 182–197)

A snapshot is the app writing itself in the snapshot view and reading itself back. One commit.

## Write
- `item.Key(context)`: what finds a value from anywhere but where it lives; null for a value that is just a value.
  The actor answers its name, the app its name. `dict.Key` and `error.Key` hide it (`new`).
- View.snapshot: `Output` writes a value's Key when it has one, else its whole face (Chooses ? Members : its Write).
- item.Members: a `[Snapshot]` member is where its value lives: it is written whole (never its Key); a held value
  whose class isn't the member's declared type is written as a record with its type (as a list row is).
- Tags: app.property/Code/actor; actor.list System/User; actor.Context; context.Variable/call; call.list.Current;
  the frame's element, property, Error, Tags, Parameter, Caller. `[Snapshot]` = what the snapshot view shows.
- New members: variable memory `list` (Held), property.list `list`, module.code `dll`/`provider`/`@default`.
- The snapshot holds the app as its `[Snapshot]` member `app`; the node tree goes.

## Read
- The snapshot's text reads back as the app's written form; resume reads it onto the sub app by the host read.
- Host read: a get-only member holding a live value reads its slot into that value; a typed record is made as its
  own type. Variables put back with the list's sync put, no events. Frames made root first from the caller chain,
  held for Resume, which enters them under the restored setting.
- Keys read back through the app: an actor by role, the app as the reading app, a goal by its key (as now).

## Goes
- ISnapshot, the walk (snapshot/this.Store.cs), every part's Store/Start, snapshot.Entries/In/Write/Read,
  module.code's registration/default records, the frames' hand list.

## Open, brought with the build
- (c) DLLs at the read; the error snapshot (the throw's frames and variables).
