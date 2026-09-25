Data — what to write, as the step writes it: a quoted text (with or without %variables% in it) is text, a bare %variable% is item.
channel — the name of a registered channel to write to, only when the step names one (`to "log" channel`, `on X channel`, `via X`). Left out otherwise: the actor's output.

- Naming a channel routes the output; it never registers one (that is channel.set).
