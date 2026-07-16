Available actions, grouped by module. The planner emits the `module.action` strings for each step; it does NOT fill parameters or decide chain order — the compiler does both. Parameter signatures and value shapes are intentionally OMITTED here; only the names matter for planning.
{% for module in modules %}{% if module.Actions.size > 0 %}
## {{ module.Name }}{% if module.Description %} — {{ module.Description }}{% endif %}

{% for a in module.Actions %}- `{{ a.Name }}`{% if a.Description %} — {{ a.Description }}{% endif %}
{% endfor %}{% endif %}{% endfor %}

# Modifiers

Modifiers wrap the preceding action in the same step. They never stand alone. Pick a modifier when the step text has a clause like `on error ...`, `cache for ...`, or `timeout after ...`.
{% for module in modules %}{% if module.Modifiers.size > 0 %}
## {{ module.Name }}

{% for a in module.Modifiers %}- `{{ a.Name }}`{% if a.Description %} — {{ a.Description }}{% endif %}
{% endfor %}{% endif %}{% endfor %}
