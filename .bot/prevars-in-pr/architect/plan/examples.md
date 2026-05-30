# Examples: build-time value transforms at the PLang level

These are illustrative — they show the shape of what the feature enables, not a fixed API. Method names (`Resize`, `orderbydesc`, `capitalize`) are placeholders for whatever the navigable surface ends up exposing. The coder/test-designer own the real names and the real `.pr`.

The pattern across all of them: the verb either *transforms the value* (rides in the parameter `value` as a navigation expression) or *acts in the world* (becomes a `module.action`).

## Shaping strings

```
- write %title%, capitalized               → output.write(content=%title.capitalize()%)
- set %slug% = %title% as a url slug         → variable.set(Value=%title.tolower().replace(' ','-')%)
- if %email% ends with "@gmail.com", ...     → condition  %email.endswith('@gmail.com')%
- set %preview% = first 100 chars of %body%  → variable.set(Value=%body.maxlength(100)%)
```

## Querying collections (the big unlock — and the frontier)

```
- set %top3% = the 3 most expensive items in %products%
        → variable.set(Value=%products.orderbydesc(price).take(3)%)
- write how many items are in %cart%         → output.write(content=%cart.count%)
- if %cart% is empty, call ShowEmpty          → condition  %cart.isempty%
- set %emails% = the email of every user      → variable.set(Value=%users.select(email)%)
- set %tags% = %rawtags% without duplicates   → variable.set(Value=%rawtags.distinct()%)
```

The first one as a `.pr` step:

```json
{
  "index": 0,
  "text": "set %top3% = the 3 most expensive items in %products%",
  "actions": [
    {
      "module": "variable", "action": "set",
      "parameters": [
        { "name": "Name",  "value": "%top3%", "type": "variable" },
        { "name": "Value", "value": "%products.orderbydesc(price).take(3)%", "type": "list<product>" }
      ],
      "modifiers": []
    }
  ],
  "formal": "variable.set(Name=%top3%, Value=%products.orderbydesc(price).take(3)%)"
}
```

The type stamp stays honest (`list<product>` in, `list<product>` out). This is LINQ-through-language, and it is where the navigable surface stops being a few string helpers and becomes a query language — flagged in plan.md as needing its own pass.

## Numbers, money, dates

```
- write %price% as currency                  → %price.tocurrency()%
- set %total% = %subtotal% rounded to 2       → %subtotal.round(2)%
- if %order.created% is older than 30 days    → condition  %order.created.daysago% > 30
- write %invoice.due% as a friendly date      → %invoice.due.friendly()%
```

## Files, paths, images

```
- if %config% exists, read it                 → condition  %config.exists%   (path's own truthiness)
- set %name% = filename of %upload% no ext     → %upload.namewithoutextension%
- set %gray% = %photo% in grayscale            → %photo.grayscale()%
- if %photo% is wider than tall, call Land     → condition  %photo.width% > %photo.height%
- write the size of %logfile% in MB            → %logfile.size.megabytes%
```

## Composition with control flow

```
- foreach the unpaid orders in %orders%, call Remind item=%order%
        → loop.foreach(items=%orders.where(unpaid)%, call=Remind, item=%order%)
```

The navigation expression *is* the collection the loop walks. Transforms compose with foreach/if for free — no separate filter step.

## Discoverability (the natural language need not match the method name)

```
- make %photo% 200 wide       → %photo.scaletowidth(200)%   (dev never typed "ScaleToWidth")
- clean up %input%            → %input.trim().collapsewhitespace()%
```

The builder feeds the type surface; the LLM maps intent → the real member. The developer speaks outcomes, not APIs.

## Where review earns its keep

```
- shrink %photo%              → %photo.resize(?,?)%   — to what?  → low confidence, build event
- the best products           → %products.???%        — by what?  → flagged for the dev
```

Vague intent surfaces as a low-confidence formal the developer corrects in review — exactly the case the confidence pipeline that just merged was built for. The build catches the ambiguity instead of the runtime guessing.
