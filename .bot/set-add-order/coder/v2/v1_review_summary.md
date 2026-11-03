# v1 review summary (codeanalyzer, b172d5123)

1. **Blocking.** When stage 1 answers its choice without probabilities, `_module` holds one module at its confidence.
   `Part` then divided it by itself (1.0), so `remove %x%` (file 0.56) went Certain. The share no module claims must
   stay doubt.
2. `Part`'s `open` list and its `All` guard were unreachable: a through-a-module pick comes from `Asked`, which drops
   settled modules.
3. Boy scout: `AssignsOne`'s `%\S+%` arm and `Placed()`'s `IndexOf` scan were a regex and a hand scan for `%…%`, when
   the parser is the one definition.
4. and 5. (each pick answering its own mark; `Take` working everything out at each ask) are older and larger, so they
   went to the backlog.
6. Naming: the architect keeps `Part`.
