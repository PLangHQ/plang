Notation: %var% = value must be a %variable% reference | type(a|b) = valid values | = val is default, param is optional
{% assign classes = actions | map: "Module" | uniq %}
{% for class_name in classes %}
# {{ class_name }}
{% for a in actions %}{% if a.Module == class_name %}
## {{ a.ActionName }}{% unless a.Cacheable %} [no-cache]{% endunless %}
{% if a.Parameters.size > 0 %}  { {% for p in a.Parameters %}{{ p.Name }}: {{ p.Value }}{% unless forloop.last %}, {% endunless %}{% endfor %} }{% else %}  (no parameters){% endif %}
{% if a.ReturnType.size > 0 %}  returns: { {% for r in a.ReturnType %}{{ r.Name }}: {{ r.Value }}{% unless forloop.last %}, {% endunless %}{% endfor %} }
{% endif %}{% for ex in a.Examples %}  e.g. `{{ ex.Name }}` → {{ ex.Value }}
{% endfor %}
{% endif %}{% endfor %}
{% endfor %}
