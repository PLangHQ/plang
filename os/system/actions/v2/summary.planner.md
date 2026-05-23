Available actions, grouped by module. The planner emits the `module.action` strings for each step; it does NOT fill parameters or decide chain order — the compiler does both. Parameter signatures and value shapes are intentionally OMITTED here; only the names matter for planning.

{% assign modules = actions | map: "Module" | uniq | sort -%}
{% for mod in modules -%}
{%- assign mod_actions = actions | where: "Module", mod -%}
{%- assign non_mod = mod_actions | where: "IsModifier", false -%}
{%- if non_mod.size > 0 -%}
{%- assign first = mod_actions | first %}

## {{ mod }}{% if first.ModuleDescription %} — {{ first.ModuleDescription }}{% endif %}

{% for a in non_mod -%}
- `{{ a.Module }}.{{ a.ActionName }}`{% if a.Description %} — {{ a.Description }}{% endif %}
{% if a.Examples.size > 0 %}{% for ex in a.Examples %}  e.g. `{{ ex.Name }}`
{% endfor %}{% endif %}{% endfor %}{% endif %}{% endfor %}

# Modifiers

Modifiers wrap the preceding action in the same step. They never stand alone. Pick a modifier when the step text has a clause like `on error ...`, `cache for ...`, or `timeout after ...`.
{% for mod in modules %}{% assign mod_actions = actions | where: "Module", mod %}{% assign mod_mods = mod_actions | where: "IsModifier", true %}{% if mod_mods.size > 0 %}{% assign first = mod_actions | first %}
## {{ mod }}

{% for a in mod_mods -%}
- `{{ a.Module }}.{{ a.ActionName }}`{% if a.Description %} — {{ a.Description }}{% endif %}
{% if a.Examples.size > 0 %}{% for ex in a.Examples %}  e.g. `{{ ex.Name }}`
{% endfor %}{% endif %}{% endfor %}{% endif %}{% endfor %}
