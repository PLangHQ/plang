# PLang User Interface Reference

Complete reference for UI rendering, interactions, and patterns.

## Overview

PLang uses HTML as its rendering engine with **Scriban** as the template engine. The UI module handles template rendering, DOM manipulation, user interactions, and navigation. All UI operations use the `[ui]` module hint for clarity.

## Client-Server Communication

PLang uses a streaming response model for efficient UI updates:

1. **First Request**: Full server-side render of the complete page
2. **Subsequent Requests**: AJAX calls with responses streamed as `application/plang+jsonl`
3. **Response Handling**: The `plang.js` client library processes each command and updates the DOM immediately

**How it works:**
- PLang intercepts all link clicks and form submissions
- Requests are automatically signed for security
- Server pushes commands (render, navigate, notify, etc.) as JSON lines
- Client executes each command in sequence, updating the UI progressively

## Links, Forms, and plang.callGoal

### URL Patterns for Links and Forms

Use route parameters in URLs instead of query strings:

```html
<!-- Preferred: ID in path -->
<a href="/product/view/{{ product.id }}">View</a>
<form action="/product/save/{{ product.id }}" method="post">

<!-- Avoid: ID in query string -->
<a href="/product/view?id={{ product.id }}">View</a>
```

Then define the route with the variable in the path:

```plang
/ GET route with parameter
- add route /product/view/%product.id%, call goal ViewProduct

/ POST route with parameter
- add route /product/save/%product.id%(number > 0), POST, call goal SaveProduct

/ DELETE route with parameter
- add route /product/delete/%product.id%(number > 0), POST, call goal DeleteProduct
```

The route parameter becomes part of the object, creating a smooth flow:

```plang
ViewProduct
/ %product.id% comes from route, then %product% gets populated by query
- select * from products where id=%product.id%, return 1, write to %product%
/ Now %product% has id (from route) + all columns from query
```

### Route Constraints

Add validation directly in the route definition:

```plang
/ Number constraint (must be positive integer)
- add route /product/view/%product.id%(number > 0), call goal ViewProduct

/ Boolean constraint (accepts true|false|1|0)
- add route /feature/%enabled%(bool), POST, call goal ToggleFeature

/ Regex constraint
- add route /product/%product.slug%(regex: ^[a-z0-9-]+$), call goal ShowProduct
```

### When to Use Standard Links

**Standard links** (`<a href="...">`) should be used for all navigation:

```html
<a href="/product/view/{{ product.id }}">View Product</a>
<a href="/dashboard">Dashboard</a>
<a href="/">Home</a>
```

### When to Use Standard Forms

**Standard forms** (`<form action="..." method="post">`) should be used for all form submissions. Plang's JS framework handles form binding automatically.

```html
<form action="/order/approve/{{ order.id }}" method="post">
    <button type="submit">Approve</button>
</form>
```

#### Form Attributes

**p-before-action** - Run JS after receiving server response but before Plang framework processes it:

```html
<form action="/process" method="post" p-before-action="validateResponse(message, context)">
    ...
</form>
```

```javascript
function validateResponse(message, context) {
    // message = server response
    // context = context information about the request
    console.log('Response received:', message);
}
```

**p-after-action** - Run JS after Plang framework has processed the response:

```html
<form action="/item/save/{{ item.id }}" method="post" p-after-action="onSaved(message, context)">
    <input type="text" name="title" value="{{ item.title }}">
    <select name="categoryId">
        <option value="">-- Select --</option>
        {{ for cat in categories }}
        <option value="{{ cat.id }}">{{ cat.name }}</option>
        {{ end }}
    </select>
    <button type="submit">Save</button>
</form>
```

**Note:** UI actions like closing dialogs should be handled server-side:

```plang
SaveItem
- update items set title=%request.body.title% where id=%item.id%
- [ui] close dialog   / closes the currently open dialog
/ or specify which dialog: - [ui] close dialog #edit-modal
```

**Execution order:**
1. Form submits to server
2. Server responds
3. `p-before-action` runs (if defined)
4. Plang framework processes the response
5. `p-after-action` runs (if defined)

### When to Use plang.callGoal

**Only use plang.callGoal when:**

1. **Identity not yet available** - On first page load when `%Identity%` doesn't exist yet

```javascript
// Landing page - first request needs identity established
document.addEventListener('DOMContentLoaded', function() {
    plang.callGoal('/auth/connect');
});
```

2. **Dynamic partial updates without a form** - When you need to update part of the page triggered by something other than a form submission

### plang.callGoal Behavior

`plang.callGoal` sends a signed request to the server. The server handles **all** UI updates - don't use `.then()` callbacks for DOM manipulation.

**Wrong - manipulating DOM in JS callback:**
```javascript
plang.callGoal('/some/goal', { data })
    .then(result => {
        document.getElementById('element').value = result.value;
    });
```

**Correct - just call the goal:**
```javascript
plang.callGoal('/some/goal', { data });
```

**Server-side Plang handles all UI updates:**
```plang
MyGoal
- process data, write to %result%
- [ui] set #element = %result.value%, #status = %result.status%
```

### Summary Table

| Scenario | Use |
|----------|-----|
| Navigate to another page | `<a href="...">` |
| Submit form | `<form action="..." method="post">` |
| Run JS after form submit | `p-after-action="..."` attribute |
| First load without identity | `plang.callGoal()` from JS |
| Dynamic update without form | `plang.callGoal()` |

## Template Engine: Scriban

PLang uses Scriban syntax for templates. Variables are accessed with double curly braces.

```html
{{ variable }}
{{ object.property }}
{{ if condition }}...{{ end }}
{{ for item in collection }}...{{ end }}
```

### Working with IDs (Important)

When working with `.id` properties on objects, IDs are `long` type and must be wrapped in quotes **in JavaScript/HTML** (not in PLang code):

```html
<!-- CORRECT: Wrap id in quotes in HTML/JavaScript -->
<a href="/product/{{ product.id }}">{{ product.name }}</a>
<input type="hidden" name="productId" value="{{ product.id }}">
<button onclick="deleteItem('{{ item.id }}')">Delete</button>
```

```plang
/ In PLang goals, NO quotes needed around variables
- set %productId% = %product.id%
- call goal DeleteItem itemId=%item.id%
```

### Common Scriban Patterns

```html
<!-- Conditionals -->
{{ if user }}
    <p>Welcome, {{ user.name }}</p>
{{ else }}
    <p>Please log in</p>
{{ end }}

<!-- Loops -->
{{ for product in products }}
    <div class="product">
        <h3>{{ product.name }}</h3>
        <span>{{ product.price | math.format "n0" }}</span>
    </div>
{{ end }}

<!-- Empty checks -->
{{ if products.size > 0 }}
    <!-- show products -->
{{ else }}
    <p>No products found</p>
{{ end }}

<!-- Formatting -->
{{ amount | math.format "n0" }}           <!-- Number: 1,234 -->
{{ date | date.to_string "%Y-%m-%d" }}    <!-- Date formatting -->
{{ text | string.truncate 100 }}          <!-- Truncate text -->

<!-- JSON output for debugging -->
{{ item | object.to_json }}
```

## Template Composition (Partials)

For maintainability, split large templates into smaller files using `{{ render "file.html" }}`.

### When to Split Templates

Split a template when:
- It exceeds ~100 lines
- It contains distinct logical sections
- Parts could be reused elsewhere

### Basic Partial Rendering

```html
<!-- product.html (main template) -->
<div class="uk-container">
    <h1>{{ product.name }}</h1>
    <p>{{ product.description }}</p>
    
    {{ render "gallery.html" }}
    {{ render "variants.html" }}
    {{ render "specs.html" }}
    {{ render "reviews.html" }}
</div>
```

The PLang goal stays simple:

```plang
Product
- select * from products where id=%id%, return 1, write to %product%
- select * from productImages where productId=%id%, write to %images%
- select * from variants where productId=%id%, write to %variants%
- select * from productSpecs where productId=%id%, write to %specs%
- select * from reviews where productId=%id%, write to %reviews%
- [ui] render "product.html", navigate
```

Each partial has access to all variables from the parent context.

## Rendering Templates

### Basic Rendering

```plang
/ Simple render
- [ui] render "template.html"

/ Render with navigation (updates browser URL, scrolls to top)
- [ui] render "template.html", navigate and scroll to top

/ Render to specific target
- [ui] render "template.html", target="#container"

/ Render with action
- [ui] render "template.html", target="#content", append
- [ui] render "template.html", target="#content", prepend
- [ui] render "template.html", target="#content" and replace
```

### Render Actions

| Action | Description |
|--------|-------------|
| `append` | Add content at end of target |
| `prepend` | Add content at beginning of target |
| `replace` | Replace target's innerHTML |
| `replaceSelf` | Replace target's outerHTML |
| `appendOrReplace` | Append content, but if element with same ID exists, replace it |
| `prependOrReplace` | Prepend content, but if element with same ID exists, replace it |

### Layout System

```plang
/ Basic layout setup - call this on webserver request
- [ui] set "layout.html" as default layout

/ With CSS framework (uikit is default)
- [ui] set "layout.html" as default layout, css framework="uikit"

/ Full configuration
- [ui] set "layout.html" as default layout, css framework="uikit", default render target="#main"
```

**Layout template:**
```html
<!DOCTYPE html>
<html>
<head>
    <script src="/js/plang.js"></script>
</head>
<body>
    <nav><!-- navigation --></nav>
    <main id="main">
        {{ main }}
    </main>
    <footer><!-- footer --></footer>
</body>
</html>
```

**Important:** 
- The `#main` element is where AJAX content gets injected
- The `{{ main }}` Scriban variable renders the initial server-side content
- Always include `/js/plang.js` - it handles client-server communication

## DOM Manipulation

### Setting Element Content

```plang
/ Set element inner HTML by ID
- set element "#message" = "<strong>Success!</strong>"
- set element "#counter" = "%count%"

/ Set element text content
- set '#status' = "Processing..."
- set '#total' = "Total: %amount% kr."

/ With variable interpolation
- set element "#greeting" = "Hello, %user.name%!"
```

### Setting Attributes

```plang
/ Set single attribute
- [ui] set checked="checked" attribute on "#checkbox"
- [ui] set disabled="disabled" attribute on "#submitBtn"
- [ui] set value="%inputValue%" attribute on "#textField"

/ Set href attribute
- [ui] set href="/product/%product.id%" attribute on "#productLink"

/ Set data attributes
- [ui] set data-id="%item.id%" attribute on "#listItem"
```

### CSS Classes

```plang
/ Add class
- set class="active" to #navItem
- set class="uk-hidden" to #modal
- set class="slide-up" to #notification
```

### Removing Elements

```plang
/ Remove element by selector
- [ui] remove '#element'
- [ui] remove '.temp-item'
- [ui] remove '#notification-%notificationId%'
```

### Show/Hide Elements

```plang
/ Show element (sets display: block)
- [ui] show '#modal'

/ Hide element (sets display: none)
- [ui] hide '#loading'
```

**Note:** For CSS framework integration, use class manipulation instead:

```plang
/ UIKit - use uk-hidden class
- set class="uk-hidden" to #element      / hide
- [ui] remove class "uk-hidden" from #element  / show
```

### UI Updates from Server

Use `[ui] set` to update DOM elements from server. Combine multiple updates for efficiency:

```plang
/ Less efficient - 3 jsonl responses
- [ui] set #name = %user.name%
- [ui] set #email = %user.email%
- [ui] set #status = "Loaded"

/ More efficient - 1 jsonl response
- [ui] set #name = %user.name%, #email = %user.email%, #status = "Loaded"
```

## User Interaction

### Ask User (Forms & Dialogs)

```plang
/ Simple form interaction
- ask user template: "form.html"
    write to %formData%

/ With callback validation
- ask user template: "signup.html"
    on callback: ValidateSignup
    write to %userData%

/ With callback data
- ask user
    render "confirm.html"
    on callback: ProcessOrder
    call back data: orderId=%orderId%, amount=%amount%
    write to %confirmation%

/ Modal dialog
- ask user template: "modal.html", open modal
    write to %result%
```

### Confirmation Dialog Pattern

```plang
ConfirmDelete
- ask user template: "confirm-delete.html"
    call back data: %item.id%
    show as modal
    write to %confirmation%
- if %confirmation.confirmed% then
    - call goal DeleteItem
```

**Note:** After the form submission, only variables defined in `call back data` exist. Using `%item.id%` directly means `%item.id%` remains available after callback.

```html
<!-- confirm-delete.html -->
<div class="uk-modal-dialog">
    <div class="uk-modal-body">
        <p>Are you sure you want to delete this item?</p>
    </div>
    <div class="uk-modal-footer uk-text-right">
        <button class="uk-button uk-button-default" type="button" name="confirmed" value="false">Cancel</button>
        <button class="uk-button uk-button-danger" type="button" name="confirmed" value="true">Delete</button>
    </div>
</div>
```

## Navigation & Window Actions

### URL Navigation

```plang
/ Navigate to URL (pushes to browser history)
- [ui] navigate to "/dashboard"

/ Navigate with state replacement (no new history entry)
- [ui] navigate to "/products", replace state

/ Render with navigate (common pattern)
- [ui] render "page.html", navigate and scroll to top
```

### Navigate Behavior

When using `navigate`, Plang:
1. Renders the template content to the default target div (e.g., `#main`)
2. Uses the JavaScript History API to update the browser URL
3. Does **not** cause a full page reload

### Scroll Control

```plang
/ Scroll to top of page
- [ui] scroll to top

/ Scroll element into view
- [ui] scroll '#section' into view

/ Focus on element
- [ui] focus '#inputField'
```

### Dialogs & Modals

```plang
/ Show modal dialog (with backdrop, blocks interaction)
- [ui] show modal '#confirmDialog'

/ Show non-modal dialog
- [ui] show dialog '#sidePanel'

/ Hide/close modal or dialog
- [ui] hide modal
- [ui] hide dialog
```

### Page Reload

```plang
- [ui] reload page
```

## Notifications & Alerts

### Toast Notifications

```plang
/ Show notification
- [ui] notify "Operation completed successfully"
- [ui] notify %message%

/ With different levels (framework-dependent)
- [ui] notify "Saved!", type: success
- [ui] notify "Warning: Low stock", type: warning
- [ui] notify "Error occurred", type: error
```

### Alert Dialog

```plang
- [ui] alert "Please confirm your action"
- [ui] alert %errorMessage%
```

## Common UI Patterns

### Loading State Pattern

```plang
ShowWithLoading
/ Show loading indicator
- [ui] render "loading.html", target="#content"
/ Fetch data
- call goal FetchData
/ Replace with actual content
- [ui] render "results.html", target="#content" and replace
```

```html
<!-- loading.html -->
<div class="uk-flex uk-flex-center uk-padding">
    <div uk-spinner="ratio: 2"></div>
</div>
```

### Progressive Enhancement

```plang
LoadProducts
- [ui] render "products-skeleton.html", target="#productList"
- select * from products where status='active', write to %products%
- [ui] render "products.html", target="#productList" and replace
```

### Infinite Scroll / Pagination

```plang
LoadMoreProducts
- set %offset% = %products.size%
- select * from products limit 20 offset %offset%, write to %moreProducts%
- [ui] render "product-items.html", target="#productList", append
```

### Tab Navigation Pattern

```plang
ShowTab
- set default value %tab% = "overview"
/ Remove active class from all tabs
- set class="" to .tab-link.active
/ Add active class to selected tab
- set class="active" to #tab-%tab%
/ Render tab content
- call goal Render%tab%
```

### Modal Pattern

```plang
OpenProductModal
- select * from products where id=%productId%, return 1, write to %product%
- [ui] render "product-modal.html"
- [ui] show '#productModal'

CloseModal
- [ui] hide '#productModal'
- [ui] remove '#productModal'
```

### Form Submission Pattern

```plang
SubmitForm
- validate %email% is not empty, "Email is required"
- validate %email% contains @, "Invalid email format"
- [ui] set disabled="disabled" attribute on "#submitBtn"
- [ui] render "submitting.html", target="#formStatus"
- call goal ProcessForm
    on error call HandleFormError
- [ui] render "success.html", target="#formStatus" and replace
- [ui] remove 'disabled' attribute from "#submitBtn"

HandleFormError
- [ui] render "error.html", target="#formStatus" and replace
- [ui] remove 'disabled' attribute from "#submitBtn"
```

### Dynamic List Updates

```plang
AddItemToList
- insert into items, name=%name%, write to %itemId%
- select * from items where id=%itemId%, return 1, write to %item%
- [ui] render "item-row.html", target="#itemList", append

RemoveItemFromList
- delete from items where id=%itemId%
- [ui] remove '#item-%itemId%'
```

### Search with Debounce (Client-Side)

```html
<input type="text" id="searchInput" class="uk-input" 
       oninput="debounceSearch(this.value)">
<div id="searchResults"></div>

<script>
let searchTimeout;
function debounceSearch(query) {
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => {
        plang.callGoal('/search/Search', { query: query });
    }, 300);
}
</script>
```

The goal handles rendering:

```plang
Search
- select * from products where name like '%' + %query% + '%' limit 20, write to %results%
- [ui] render "search-results.html", target="#searchResults"
```

## Working with UIKit

UIKit is the recommended CSS framework for PLang applications.

### Common UIKit Components

```html
<!-- Cards -->
<div class="uk-card uk-card-default uk-card-body">
    <h3 class="uk-card-title">{{ title }}</h3>
    <p>{{ content }}</p>
</div>

<!-- Tables -->
<table class="uk-table uk-table-striped uk-table-hover">
    <thead>
        <tr><th>Name</th><th>Price</th></tr>
    </thead>
    <tbody>
        {{ for item in items }}
        <tr>
            <td>{{ item.name }}</td>
            <td>{{ item.price | math.format "n0" }}</td>
        </tr>
        {{ end }}
    </tbody>
</table>

<!-- Alerts -->
<div class="uk-alert uk-alert-success" uk-alert>
    <a class="uk-alert-close" uk-close></a>
    <p>{{ message }}</p>
</div>

<!-- Modal -->
<div id="myModal" uk-modal>
    <div class="uk-modal-dialog uk-modal-body">
        <button class="uk-modal-close-default" type="button" uk-close></button>
        <h2 class="uk-modal-title">{{ title }}</h2>
        <p>{{ content }}</p>
    </div>
</div>
```

### UIKit Forms

```html
<form class="uk-form-stacked">
    <div class="uk-margin">
        <label class="uk-form-label">Email</label>
        <div class="uk-form-controls">
            <input class="uk-input" type="email" name="email" required>
        </div>
    </div>
    
    <div class="uk-margin">
        <label class="uk-form-label">Category</label>
        <div class="uk-form-controls">
            <select class="uk-select" name="category">
                {{ for cat in categories }}
                <option value="{{ cat.id }}">{{ cat.name }}</option>
                {{ end }}
            </select>
        </div>
    </div>
    
    <button class="uk-button uk-button-primary" type="submit">Submit</button>
</form>
```

### UIKit Grid System

```html
<!-- Basic grid -->
<div class="uk-grid" uk-grid>
    <div class="uk-width-1-2">Half</div>
    <div class="uk-width-1-2">Half</div>
</div>

<!-- Responsive grid -->
<div class="uk-grid uk-child-width-1-2@s uk-child-width-1-3@m" uk-grid>
    {{ for product in products }}
    <div>{{ render "product-card.html" }}</div>
    {{ end }}
</div>
```

### UIKit Utility Classes

```html
<!-- Spacing -->
<div class="uk-margin">...</div>
<div class="uk-padding">...</div>

<!-- Text -->
<p class="uk-text-lead">Lead paragraph</p>
<span class="uk-text-bold">Bold</span>
<span class="uk-text-danger">Error text</span>
<span class="uk-text-success">Success text</span>

<!-- Alignment -->
<div class="uk-text-center">Centered</div>

<!-- Flex -->
<div class="uk-flex uk-flex-between uk-flex-middle">
    <span>Left</span>
    <span>Right</span>
</div>
```

## Complete Actions Reference

All available UI actions that can be used with render or standalone:

| Action | Description |
|--------|-------------|
| `replace` | Replace target's innerHTML |
| `replaceSelf` | Replace target's outerHTML |
| `append` | Append content to target |
| `prepend` | Prepend content to target |
| `appendOrReplace` | Append, or replace existing element with same ID |
| `prependOrReplace` | Prepend, or replace existing element with same ID |
| `scrollIntoView` | Scroll target element into view |
| `scrollToTop` | Scroll page to top |
| `focus` | Focus on target element |
| `show` | Show element (display: block) |
| `hide` | Hide element (display: none) |
| `showModal` | Open target as modal dialog |
| `showDialog` | Open target as non-modal dialog |
| `hideModal` | Close current modal |
| `hideDialog` | Close current dialog |
| `notify` | Show notification (uses UIKit notification) |
| `alert` | Show browser alert dialog |
| `navigate` | Push URL to browser history (no full page reload) |
| `replaceState` | Replace current URL in browser history |
| `reload` | Reload the page |

## File Organization

Templates live in a `templates` folder alongside the goals that use them:

### Single Goal in Folder

```
/product
  Product.goal
  /templates
    product.html
    variants.html
    specs.html
```

### Multiple Goals in Folder

```
/admin
  Products.goal
  Users.goal
  /templates
    /products
      list.html
      edit.html
    /users
      list.html
      detail.html
```

### Guidelines

- **Keep templates close to goals** - easier to find and maintain
- **Flat structure when possible** - only create subfolders when needed
- **Shared templates** - put in a `/shared/templates` folder
- **Layouts are global** - keep in a root `/layouts` folder

## User Identity and Protected Routes

The first request to the server is unsigned, so `%Identity%` is not available. To access user identity, use the `on connect` event:

```plang
Start
- start webserver, on connect call UserConnects

UserConnects
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user.role% contains "admin" then call RenderAdminNav
```

## Best Practices

1. **Use the `[ui]` module hint** for clarity in UI operations
2. **Split large templates** using `{{ render "partial.html" }}` - keep under 100 lines
3. **Separate concerns**: Keep templates simple, business logic in goals
4. **Use layouts** for consistent page structure
5. **Show loading states** for async operations
6. **Validate before submit** to improve user experience
7. **Use semantic HTML** and accessibility attributes
8. **Prefer replace over remove+append** for smoother updates
9. **Handle errors gracefully** with user-friendly messages
10. **Let server decide rendering** - don't manipulate DOM in JS callbacks
