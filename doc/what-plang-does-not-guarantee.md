# What PLang does not guarantee

For someone deciding whether to trust PLang. The question this page answers: where do the other trust pages stop?

Each point is something a reader might assume and cannot rely on today.

- **A saved key is not encrypted at rest.** The saved decider key was found in plain text in `.data/setting/data.sqlite`, in a file readable by any user of the machine. The operating system's file permissions are the protection. See [keys](keys.md).
- **A goal can print a key.** A step that writes a key variable with `write out` prints it. A sensitive marking in the source is not a promise of silence. See [keys](keys.md).
- **A built step may not do what you wrote.** A build that ends `Saved` does not prove the step means what you meant. Two builds of vague steps ended `Saved` with exit 0; one then stopped at run time with a `NullReferenceException`. Read the output of a first run. See [when a step fails](when-a-step-fails.md).
- **The model may not be shown your variable's value.** A variable inside a model message was sent unreplaced. Do not assume a value reaches, or does not reach, the model. See [what leaves the machine](what-leaves-the-machine.md).
- **A build sends your steps to a service.** The text of each unbuilt step goes to the decider service. File contents and the app's path did not appear in the one request read. That is one request, not a promise about all of them.
- **The prompt does not protect you from the app.** An app can answer its own questions and run as the System actor. See [code from elsewhere](code-from-elsewhere.md).
- **No network proof.** The runs saw no request beyond those named on [what leaves the machine](what-leaves-the-machine.md). That is not a packet capture.
- **Windows and macOS were not run.** Every run behind these pages was on Linux. File modes and path behaviour may differ.
- **The `y` answer lasts one run.** The saved `a` answer lasts across runs, until it is revoked. How to revoke one from a goal was not run.
- **No certification or compliance claim.** Nothing here says PLang meets a standard or a law.
- **Not every statement has a test.** Where a page says a test was not found, that is a gap, not a proof of safety.

When a statement here changes because the code changed, [verify.md](verify.md) has the date-less row that says how it was checked. Re-run it.
