Message — the error's message: the quoted text after `throw`.
Data — the values the error carries: the %variables% after `throw` (`throw %!error%` re-raises that error).
Status — the error's status (a number like 404), only when the step gives one.
Key — the error's key, only when the step names one.
FixSuggestion — how the programmer fixes it: the text after `fix suggestion`.

- `throw %!error%` re-raises that error: `Data=%!error%`, never `Message=%!error%`. Message is for a new error's words.
