# The Windows run (bugs 43)

The `bugs` branch is ready to merge into runtime2, but it hasn't run on Windows. Its Windows window (`screen/type/screen/window/code/Window.cs`) has only been compiled (`-r win-x64`), never opened, and the path and store tests have never run on a case-blind disk. This run is the last thing before the merge (bugs 22).

On the Windows machine, at `bugs` 769abdcb6 or later. No OpenAI key is needed.

1. **Build:** `dotnet msbuild PLang.Tests/All.proj /t:Restore;Build /p:Configuration=Debug`
2. **The C# sweep:** run `PLang.Tests\<P>\bin\Debug\net10.0\PLang.Tests.<P>.exe` for each P in Data, Generator, Modules, Runtime, Types, Wire. On Linux only Wire's 6 snapshot tests fail. Any other failure is a finding.
   - Paths and stores on a case-blind disk: LinkSpotTests (the junction), StoresFolderTests, RealPathGateReCheckProbeTests, SubAppContainmentProbeTests, SubAppStoresShapesProbeTests, SubAppRootTests, StoreStandingProbeTests, ValidatePathTests, RootComparisonKeyingTests, PathEqualityTests.
   - The window's frames: WindowFrameTests, ShownTests, FrameBoundTests, FrameAmplificationProbeTests.
   - One class alone: `PLang.Tests.Runtime.exe --treenode-filter "/*/*/*LinkSpotTests*/*"`
3. **plang's tests:** `cd test` then `..\PlangConsole\bin\Debug\net10.0\plang.exe --test`. On Linux, BareLiteralJudgedByForm fails and 3 are stale. Any other failure is a finding.
4. **The real window:** show PlangOS's screen in the Windows window the way you normally do. It should open, draw, update, take a click and a key, and close. A patch that comes before the first whole picture (APatchBeforeTheFirstFrame) lands on the blank canvas: no crash, no garbage.

**Report** to the architect: the commit, each finding (the test's name, its message, the first lines of its stack), and what step 4 showed. A folder link is removed with `Directory.Delete(p, recursive: false)`, so the worst a failing link test can do is a delete that fails. Contents are never deleted.
