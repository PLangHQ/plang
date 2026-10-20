# test-speed v1 — plan

Branch cut from item-doors (b2354e747) by decision 159: a playground for speeding up the C# tests and app birth.
Nothing lands on item-doors until Ingi says so.

1. Carry 157 here: `format<T>` binds T's own IEncode/IDecode on first use (static per T, kept as written —
   the architect noted "no statics"; here we try things); registry helpers Encoder/Bound/Decoder/Reading gone;
   kind's encode/decode ctor params and delegate types gone; `FormatWritesItselfTests` (red first); surface re-pinned.
2. §6 of `test-speed.md`: the code's facts read once per process — measure what a birth costs when the class walks
   (GetTypes, NameOf, [Format]/[Choice], kind classes, module catalog) are shared, against Ingi's ~5 ms prediction.
3. Re-time the suites after each step; report birth median before → after.
