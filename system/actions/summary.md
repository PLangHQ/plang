Notation: @var = value is a %variable% reference | type(a|b) = valid values | = val is default, param is optional
{{ classes = actions | array.map "Module" | array.uniq }}
{{ for class_name in classes }}
# {{ class_name }}
{{ for a in actions }}{{ if a.Module == class_name }}
## {{ a.ActionName }}{{ if !a.Cacheable }} [no-cache]{{ end }}
{{ if a.Parameters.size > 0 }}  { {{ for p in a.Parameters }}{{ p.Name }}: {{ p.Value }}{{ if !for.last }}, {{ end }}{{ end }} }{{ else }}  (no parameters){{ end }}
{{ end }}{{ end }}
{{ end }}