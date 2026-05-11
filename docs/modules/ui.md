# UI Module

Render Liquid templates with variable interpolation, includes, and goal execution. Uses the [Fluid](https://github.com/sebastienros/fluid) template engine.

## Actions

### render

Render a Liquid template — inline content or a file.

```plang
/ Render a template file
- render 'email.html', write to %body%

/ Render with explicit parameters
- render 'page.html' with title=%pageTitle%, write to %html%

/ Render inline content
- render %templateContent%, write to %result%

/ Force file mode (error if not found)
- render file 'report.html', write to %output%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Template | string | yes | — | Liquid template content (inline) or file path |
| Parameters | list | no | — | Explicit variables that override memory stack values in the template |
| IsFile | bool | no | null | `true` = file path, `false` = inline content, `null` = auto-detect |

**Returns:** The rendered string.

## Template Syntax

PLang uses [Liquid syntax](https://shopify.github.io/liquid/). Variables from your PLang memory stack are available automatically.

### Variables

```plang
- set %name% = 'World'
- set %count% = 42
- render 'Hello {{ name }}! You have {{ count }} items.', write to %result%
/ Result: "Hello World! You have 42 items."
```

### Explicit Parameters

Override memory stack variables or add template-specific values:

```plang
- set %name% = 'Default'
- render 'page.html' with name='Override', year=2026, write to %html%
/ In the template, {{ name }} is "Override", not "Default"
```

### Conditionals and Loops

```liquid
{% if items.size > 0 %}
<ul>
  {% for item in items %}
    <li>{{ item.name }} — {{ item.price }}</li>
  {% endfor %}
</ul>
{% else %}
  <p>No items found.</p>
{% endif %}
```

### Includes

Split templates into reusable partials with `{% include %}`:

```liquid
{% include 'header.html' %}
<main>{{ content }}</main>
{% include 'footer.html' %}
```

Include paths resolve relative to the calling goal's directory. The file must exist within the PLang sandbox.

### Calling Goals

Execute a PLang goal from within a template:

```liquid
<p>Latest data: {% callGoal 'GetLatestStats' %}</p>
```

The goal runs and its return value is inserted into the template output. If the goal fails, an `[Error: ...]` message appears inline.

## File vs Inline Detection

When `IsFile` is not set (null), the module auto-detects:

- Contains `{{` or `{%` → treated as inline Liquid content
- Has a file extension (e.g., `.html`, `.liquid`) → treated as a file path
- Otherwise → treated as inline content

Set `IsFile` explicitly to override:

```plang
/ Force file mode — errors if file not found
- render file 'template.html', write to %html%

/ Force inline mode — even if it looks like a file path
- render 'some.text' as inline, write to %result%
```

## HTML Encoding

Output is HTML-encoded by default for XSS prevention. `{{ name }}` where name is `<script>alert(1)</script>` renders as `&lt;script&gt;alert(1)&lt;/script&gt;`.

Use the Liquid `raw` filter if you need unescaped HTML from a trusted source.

## Security

**Never pass untrusted user input as the Template parameter.** Because templates can execute goals via `{% callGoal %}`, passing user-controlled content as the template string is a server-side template injection (SSTI) vulnerability.

Safe:
```plang
/ Template is a file you control
- render 'email.html' with message=%userInput%, write to %body%
```

Unsafe:
```plang
/ DANGEROUS — user controls the template itself
- render %userInput%, write to %body%
```

If you need to render user-provided markup, treat it as a variable *inside* a template you control — never as the template itself.

## Custom Engine

The template engine is swappable. Load a DLL that implements `ITemplate` to replace Fluid with another engine:

```plang
- load code 'my-template-engine.dll'
```

## Examples

### Render an Email

```plang
Start
- set %user% = {name: "Alice", plan: "Pro"}
- render 'templates/welcome.html' with user=%user%, write to %emailBody%
- write out %emailBody%
```

`templates/welcome.html`:
```liquid
<h1>Welcome, {{ user.name }}!</h1>
<p>Your plan: {{ user.plan }}</p>
```

### Render with Includes

```plang
Start
- set %title% = 'Dashboard'
- render 'pages/dashboard.html', write to %html%
- write out %html%
```

`pages/dashboard.html`:
```liquid
{% include 'layout/header.html' %}
<h1>{{ title }}</h1>
{% include 'layout/sidebar.html' %}
{% include 'layout/footer.html' %}
```

### Dynamic Content via Goal

```plang
Start
- render 'status-page.html', write to %html%

GetServerStatus
- http request 'https://api.example.com/status', write to %status%
- return %status.message%
```

`status-page.html`:
```liquid
<div class="status">
  {% callGoal 'GetServerStatus' %}
</div>
```
