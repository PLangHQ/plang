# Keys

For someone deciding whether to trust PLang. The question this page answers: where do my keys live, and who can see them?

PLang uses two keys of the service kind: the **decider key** for the build (`TYPESAFE_API_KEY` or `%!llm.decider.setting.key%`) and the **model key** for steps that ask a model at run time (`OPENAI_API_KEY` or `%!llm.setting.key%`). See [install](install.md#the-key-a-build-needs).

## Where they come from

A key is read from the app's saved setting, else from the environment variable. With none held, a build asks once for the decider key, and a run that needs the model asks for the model key (`MissingLlmKey (401)` when no one answers). The answer is saved in the app.

## Where a saved key is kept

In the app's settings store, `.data/setting/data.sqlite`, under the app's folder.

- A search of that file for the saved decider key found it, in plain text. It is not encrypted there. (Checked by searching the file for the key from the file it came from, without displaying either.)
- The file's mode on Linux was `rw-r--r--`, readable by any user on the machine who can reach the folder.
- Another actor inside the app is refused reads of `.data/` (see [files and permissions](files-and-permissions.md)). The operating system's file permissions are what stop another user of the machine.

Whether the identity's private key is stored the same way was not checked.

## What is printed

A key declared sensitive in the source is documented as never shown (`PLang/app/module/llm/setting/this.cs:19-21` and `PLang/app/module/llm/decider.setting.cs:20-23`). In practice, a goal that writes a key variable with `write out` built, ran, and printed that key in full to the output. Read an app's goals before you run it, and look for any step that writes a key variable. This is recorded as a failure of the "never shown" claim in [verify.md](verify.md). It is with the security owners to assess.

What does not print, as seen: the build's own output, the permission questions, and error messages did not contain a key in any run for these pages.

## What is sent with a key

A key travels in an `Authorization: Bearer …` header to the service it belongs to, and nowhere else seen. See [what leaves the machine](what-leaves-the-machine.md).

## Tests

None was found for the claim that a saved key is stored as plain text or that a goal can print it. The statements above come from runs.
