Message — the error's message: ONLY a quoted text literal after `throw` (`throw "checkout failed"`). Never a %variable%; a variable never goes in Message.
Data — the values the error carries: the %variables% after `throw` (`throw %!error%` re-raises that error). Every variable goes here, never also in Message — except the one after the word `cause`.
Cause — the errors that caused this one; the thrown error holds them whole · say: `cause %errors%` · builder: the %variable% after the word `cause`, a list of errors; it is Cause, never Data.
Status — the error's status (a number like 404), only when the step gives one.
Key — the error's key, only when the step names one.
FixSuggestion — how the programmer fixes it: the text after `fix suggestion`.

- `throw %!error%` re-raises that error: `Data=%!error%` **alone** — never `Message=%!error%`, and never both. Message is a quoted literal for a new error's words; a variable is never duplicated into it.
