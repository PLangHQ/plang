# Trust

For someone deciding whether to trust PLang, and for an auditor or security reviewer. This set answers: what does PLang do with my files, my keys and my network, and what does it not do?

Each page states what was run or read at a source line, and names the test that holds it where one exists. Where no test holds a statement, the page says so. Nothing here is a legal or compliance claim.

1. [Files and permissions](files-and-permissions.md): what a program may read and write, and when it is asked.
2. [Keys](keys.md): where keys live and who can see them.
3. [What leaves the machine](what-leaves-the-machine.md): the build's calls, the model service, http.
4. [Identity and signing](identity-and-signing.md): what is signed, and what `identity.export` refuses.
5. [Code from elsewhere](code-from-elsewhere.md): what a goal or an app you did not write can and cannot do.
6. [What PLang does not guarantee](what-plang-does-not-guarantee.md): stated plainly.

Read page 6 even if you read nothing else.

Every claim is in [verify.md](verify.md), with how it was checked.
