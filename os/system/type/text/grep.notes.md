grep — The lines that match a pattern, each returned as "line number: line", with context lines around each match when asked. · say: %text.grep("TODO")%
pattern — what each line is matched against · say: the first argument
lines — how many lines of context to keep around each match; 0 (none) by default · say: the second argument, a number (leave it out for none)
Returns — a list of the matching lines (list<text>).
