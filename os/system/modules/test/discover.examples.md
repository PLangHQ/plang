Step text: `discover tests in 'Tests/Foo', not its subfolders, write to %tests%`
Formal: `test.discover(Path: path = "Tests/Foo", Subfolder: choice<subfolder> = "skip")`

Step text: `discover tests in '_fixtures', pattern='*.fixture.goal'`
Formal: `test.discover(Path: path = "_fixtures", Pattern: text = "*.fixture.goal")`
