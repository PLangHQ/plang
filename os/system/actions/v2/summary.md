Actions below map PLang step text to engine calls. Each entry: `/ description` then `- module.action Params`.
Notation: `?` = optional, `=val` = default, `%var%` = runtime variable reference.
{% assign modules = actions | map: "Module" | uniq | sort -%}
{% for mod in modules -%}
{%- assign mod_actions = actions | where: "Module", mod -%}
{%- assign non_mod = mod_actions | where: "IsModifier", false -%}
{%- if non_mod.size > 0 -%}
{%- assign first = mod_actions | first %}

## {{ mod }}{% if first.ModuleDescription %} — {{ first.ModuleDescription }}{% endif %}

{% for a in non_mod %}{% if a.Description %}/ {{ a.Description }}
{% endif %}- {{ a.Module }}.{{ a.ActionName }}{% if a.Parameters.size > 0 %} {% for p in a.Parameters %}{{ p.Name }}([{{ p.Value }}]){% unless forloop.last %}, {% endunless %}{% endfor %}{% endif %}{% if a.Examples.size > 0 %}{% for ex in a.Examples %}
  e.g. `{{ ex.Name }}` → {{ ex.Value }}{% endfor %}{% endif %}

{% endfor %}{% endif %}{% endfor %}

# Modifiers

Modifiers wrap the preceding action in the same step. They never stand alone — a step that starts with a modifier is invalid. In `formal`, a modifier appears as an extra pipe segment right after the action it wraps: `file.read Path(...) | cache.wrap DurationMs(...)`.
{% for mod in modules %}{% assign mod_actions = actions | where: "Module", mod %}{% assign mod_mods = mod_actions | where: "IsModifier", true %}{% if mod_mods.size > 0 %}{% assign first = mod_actions | first %}
## {{ mod }}{% if first.ModuleDescription %} — {{ first.ModuleDescription }}{% endif %}

{% for a in mod_mods %}{% if a.Description %}/ {{ a.Description }}
{% endif %}- {{ a.Module }}.{{ a.ActionName }}{% if a.Parameters.size > 0 %} {% for p in a.Parameters %}{{ p.Name }}([{{ p.Value }}]){% unless forloop.last %}, {% endunless %}{% endfor %}{% endif %}{% if a.Examples.size > 0 %}{% for ex in a.Examples %}
  e.g. `{{ ex.Name }}` → {{ ex.Value }}{% endfor %}{% endif %}

{% endfor %}{% endif %}{% endfor %}
