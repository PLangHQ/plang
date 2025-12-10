# PLang UI Interactions Reference

User interaction, navigation, dialogs, and notifications.

**Related references:**
- [user-interface.md](user-interface.md) - Core UI concepts, templates, rendering, DOM manipulation
- [ui-patterns.md](ui-patterns.md) - Common UI patterns, UIKit components, actions reference

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

### Form Template Example

```html
<!-- form.html -->
<form id="userForm">
    <div class="uk-margin">
        <label>Email</label>
        <input type="email" name="email" class="uk-input" required>
    </div>
    <div class="uk-margin">
        <label>Message</label>
        <textarea name="message" class="uk-textarea"></textarea>
    </div>
    <button type="submit" class="uk-button uk-button-primary">Submit</button>
</form>
```

### Confirmation Dialog Pattern

```plang
ConfirmDelete
- ask user template: "confirm-delete.html"
    call back data: itemId=%item.id%
    write to %confirmation%
- if %confirmation.confirmed% then
    - call goal DeleteItem itemId=%item.id%
```

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
| `navigate` | Push URL to browser history |
| `replaceState` | Replace current URL in browser history |
| `reload` | Reload the page |