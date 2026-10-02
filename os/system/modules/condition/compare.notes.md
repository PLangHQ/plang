Left — the first value compared · say: the value before the comparison · builder: as the step writes it
Operator — the comparison, a choice<operator> · say: `>`, `is`, `contains`, `is empty`, … (as in condition.if) · builder: the same operator mapping as condition.if — see if.notes' table; a negation is its own operator
Right — the second value · say: the value after the comparison · builder: left out for isempty / isnotempty

- compare answers a yes/no and hands it on: `compare %a% > %b%, write to %isGreater%`; the `write to %x%` is its own action.
- The subject's own module is not involved just because the question is about its contents (a question about a file is still a compare, not a file action).
