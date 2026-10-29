Step text: `discover tests in 'Tests/Foo', not its subfolders, write to %tests%`
Formal: `test.discover(Path="Tests/Foo", Subfolder="skip")`

Step text: `discover tests in '_fixtures', pattern='*.fixture.goal'`
Formal: `test.discover(Path="_fixtures", Pattern="*.fixture.goal")`
