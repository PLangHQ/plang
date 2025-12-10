# PLang UI Patterns Reference

Common UI patterns and UIKit component examples.

**Related references:**
- [user-interface.md](user-interface.md) - Core UI concepts, templates, rendering, DOM manipulation
- [ui-interactions.md](ui-interactions.md) - User interaction, navigation, dialogs, notifications

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

**Important:** Always let the server-side goal decide where to render content. The JavaScript just calls the goal - the goal determines the target, action, and whether to show as modal.

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
        <tr>
            <th>Name</th>
            <th>Price</th>
        </tr>
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

<!-- Dropdown -->
<div class="uk-inline">
    <button class="uk-button uk-button-default" type="button">Menu</button>
    <div uk-dropdown>
        <ul class="uk-nav uk-dropdown-nav">
            {{ for item in menuItems }}
            <li><a href="{{ item.url }}">{{ item.label }}</a></li>
            {{ end }}
        </ul>
    </div>
</div>
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
    <div>
        {{ render "product-card.html" }}
    </div>
    {{ end }}
</div>

<!-- Grid with gap -->
<div class="uk-grid uk-grid-small" uk-grid>
    <!-- content -->
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
    
    <div class="uk-margin">
        <label>
            <input class="uk-checkbox" type="checkbox" name="terms"> 
            Accept terms
        </label>
    </div>
    
    <button class="uk-button uk-button-primary" type="submit">Submit</button>
</form>
```

### UIKit Visibility Classes

```plang
/ Hide element using UIKit class (preferred over inline styles)
- set class="uk-hidden" to #element

/ Show element by removing the hidden class
- [ui] remove class "uk-hidden" from #element
```

### UIKit Utility Classes

```html
<!-- Spacing -->
<div class="uk-margin">...</div>
<div class="uk-margin-large">...</div>
<div class="uk-padding">...</div>

<!-- Text -->
<p class="uk-text-lead">Lead paragraph</p>
<p class="uk-text-meta">Meta text</p>
<p class="uk-text-muted">Muted text</p>
<span class="uk-text-bold">Bold</span>
<span class="uk-text-danger">Error text</span>
<span class="uk-text-success">Success text</span>

<!-- Alignment -->
<div class="uk-text-center">Centered</div>
<div class="uk-text-right">Right aligned</div>

<!-- Flex -->
<div class="uk-flex uk-flex-between uk-flex-middle">
    <span>Left</span>
    <span>Right</span>
</div>

<!-- Width -->
<div class="uk-width-1-1">Full width</div>
<div class="uk-width-1-2@m">Half on medium+</div>
<div class="uk-width-auto">Auto width</div>
```

### UIKit Icons

```html
<!-- Basic icon -->
<span uk-icon="check"></span>
<span uk-icon="icon: star; ratio: 1.5"></span>

<!-- Icon in button -->
<button class="uk-button uk-button-default">
    <span uk-icon="plus"></span> Add Item
</button>

<!-- Icon link -->
<a href="#" uk-icon="icon: trash" title="Delete"></a>
```

## Responsive Patterns

### Hide/Show by Screen Size

```html
<!-- Hide on small screens -->
<div class="uk-visible@m">Only visible on medium and up</div>

<!-- Show only on small screens -->
<div class="uk-hidden@m">Only visible on small</div>
```

### Responsive Navigation

```html
<nav class="uk-navbar-container" uk-navbar>
    <div class="uk-navbar-left">
        <a class="uk-navbar-item uk-logo" href="/">Logo</a>
        
        <!-- Desktop menu -->
        <ul class="uk-navbar-nav uk-visible@m">
            {{ for item in navItems }}
            <li><a href="{{ item.url }}">{{ item.label }}</a></li>
            {{ end }}
        </ul>
    </div>
    
    <div class="uk-navbar-right">
        <!-- Mobile menu toggle -->
        <a class="uk-navbar-toggle uk-hidden@m" uk-navbar-toggle-icon 
           uk-toggle="target: #mobile-nav"></a>
    </div>
</nav>

<!-- Mobile menu -->
<div id="mobile-nav" uk-offcanvas>
    <div class="uk-offcanvas-bar">
        <ul class="uk-nav uk-nav-default">
            {{ for item in navItems }}
            <li><a href="{{ item.url }}">{{ item.label }}</a></li>
            {{ end }}
        </ul>
    </div>
</div>
```

## Error Handling Patterns

### Form Validation Feedback

```html
<!-- Error state -->
<div class="uk-margin">
    <label class="uk-form-label">Email</label>
    <div class="uk-form-controls">
        <input class="uk-input uk-form-danger" type="email" name="email">
        <span class="uk-text-danger uk-text-small">Please enter a valid email</span>
    </div>
</div>

<!-- Success state -->
<div class="uk-margin">
    <label class="uk-form-label">Email</label>
    <div class="uk-form-controls">
        <input class="uk-input uk-form-success" type="email" name="email">
    </div>
</div>
```

### Error Page Template

```html
<!-- error.html -->
<div class="uk-section uk-section-muted">
    <div class="uk-container uk-container-small uk-text-center">
        <h1 class="uk-heading-small">{{ error.title | default "Something went wrong" }}</h1>
        <p class="uk-text-lead uk-text-muted">{{ error.message }}</p>
        <a href="/" class="uk-button uk-button-primary">Go Home</a>
    </div>
</div>
```

### Empty State Template

```html
<!-- empty-state.html -->
<div class="uk-text-center uk-padding-large">
    <span uk-icon="icon: inbox; ratio: 3" class="uk-text-muted"></span>
    <h3 class="uk-margin-top">No items yet</h3>
    <p class="uk-text-muted">{{ message | default "Get started by adding your first item" }}</p>
    {{ if showAction }}
    <a href="{{ actionUrl }}" class="uk-button uk-button-primary uk-margin-top">
        {{ actionLabel | default "Add Item" }}
    </a>
    {{ end }}
</div>
```