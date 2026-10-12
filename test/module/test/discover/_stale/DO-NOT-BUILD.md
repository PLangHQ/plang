# Keep `unbuilt.fixture.goal` UNBUILT

`unbuilt.fixture.goal` must have **no** `.pr` in `.build/`.

`TestDiscoverReportsStaleWhenPrMissing` depends on it: the discoverer finds
the two fixtures here and the test asserts one of them is reported Stale with
"no .pr". With no `.build/unbuilt.fixture.pr`, that is true and the test
passes. Building this fixture would give it a `.pr`, so it would no longer be
reported Stale and the test would **fail**.

`ready.fixture.goal` in this folder *is* built on purpose — it is the
built-counterpart the same test compares against.

Do not add `unbuilt.fixture.goal` to any `--build` file list or whole-tree
rebuild. If a rebuild pass ever creates `.build/unbuilt.fixture.pr`, delete it.
