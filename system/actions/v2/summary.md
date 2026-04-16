Format: `- module.action ParamName([type] value)` where ? = optional, = val is default, %var% = variable reference
Types: tstring = translatable string — use for any user-facing text (output, messages, labels). When a variable resolves to a string at runtime, it becomes tstring automatically.

Example: `- goal.call GoalName([goal.call] {"name":"Greet","parameters":[{"name":"x","value":1}]})` becomes:
```json
{"module":"goal","action":"call","parameters":[{"name":"GoalName","value":{"name":"Greet","parameters":[{"name":"x","value":1}]},"type":"goal.call"}]}
```

Actions:
{% assign classes = actions | map: "Module" | uniq %}{% for class_name in classes %}
{% for a in actions %}{% if a.Module == class_name %}- {{ a.Module }}.{{ a.ActionName }}{% unless a.Cacheable %} [no-cache]{% endunless %} {% if a.Parameters.size > 0 %}{% for p in a.Parameters %}{{ p.Name }}([{{ p.Value }}]){% unless forloop.last %}, {% endunless %}{% endfor %}{% else %}(no parameters){% endif %}{% if a.ReturnType.size > 0 %} => {% for r in a.ReturnType %}{{ r.Name }}:{{ r.Value }}{% unless forloop.last %}, {% endunless %}{% endfor %}{% endif %}
{% endif %}{% endfor %}{% endfor %}
