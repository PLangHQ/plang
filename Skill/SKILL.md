---
name: plang
description: Expert guidance for Plang programming language (plang.is). Use when the user asks about Plang syntax, wants to generate Plang code, needs help debugging Plang goals, wants to understand Plang patterns, or is working on Plang projects. Plang is a natural language pseudo-code language with goal-based architecture using SQLite databases.
---

# Plang Programming Language

## Overview

Plang is a natural language programming language that uses pseudo-code syntax and goal-based architecture. Each file represents a "goal" containing steps written in natural language. Use this skill when working with Plang code generation, debugging, or architectural guidance.

## Quick Start

For comprehensive syntax and examples, read the appropriate reference files:
- **Basic syntax**: Read [syntax.md](references/syntax.md) for variable handling, database operations, HTTP requests, control flow
- **Common patterns**: Read [patterns.md](references/patterns.md) for proven implementation patterns
- **Database schemas**: Read [database.md](references/database.md) for table creation and data modeling

## Core Concepts

### Goal Structure

Goals are Plang's fundamental unit. Each goal is a file containing steps:

```plang
GoalName
- step 1 description
- step 2 description  
- step 3 description
```

### Natural Language Steps

Steps are written in natural language with specific patterns Plang recognizes:

```plang
ProcessOrder
- select * from orders where id=%orderId%, return 1, write to %order%
- if %order% is empty then
  - throw "Order not found"
- call goal ValidateOrder
- update orders set status='processed' where id=%orderId%
```

### Key Design Principles

1. **SQLite-first**: Presume SQLite for all database operations unless specified
2. **Goal-based**: Break functionality into discrete goals (files)
3. **Natural language**: Write steps as readable instructions
4. **Variable passing**: Use `%variableName%` syntax throughout
5. **Error handling**: Use `on error` clauses and validation

## When to Generate Plang Code

Generate Plang goals when the user:
- Asks to "create a Plang goal for..."
- Requests database operations (Plang excels at CRUD)
- Needs API integration code
- Wants to build web applications with UI
- Requires LLM integration
- Needs file processing workflows
- Wants event-driven architectures

## Code Generation Guidelines

### 1. Start with Clear Goal Names

```plang
CreateUser          ✓ Clear, action-oriented
ProcessPayment      ✓ Verb + noun pattern
GetUserData         ✓ Specific purpose
DoStuff             ✗ Too vague
Main                ✗ Not descriptive
```

### 2. Use Proper Variable Syntax

```plang
✓ %userId%
✓ %order.id%
✓ %user.email%
✓ %request.body.name%
✓ %Settings.ApiKey%
✓ %now%

✗ userId (missing % delimiters)
✗ %user-id% (use camelCase or underscores)
```

### 3. Follow Database Conventions

```plang
✓ - select * from users where id=%userId%, return 1, write to %user%
✓ - insert into orders, status='pending', amount=%total%, write to %orderId%
✓ - update products set price=%newPrice% where id=%productId%

✗ - query the users table (too vague)
✗ - get user by id (missing proper syntax)
```

### 4. Structure Complex Goals Properly

For complex operations, break into sub-goals:

```plang
ProcessOrder
- call goal ValidateOrder
- call goal CalculateTotal
- call goal CreateTransaction
- call goal SendConfirmation
```

### 5. Use Transactions Appropriately

```plang
ProcessPayment
- begin transaction "users/%userId%"
- insert into orders ...
- insert into orderItems ...
- call goal ChargeCard
    on error call RollbackOrder
- end transaction
```

## Common Task Patterns

### Database CRUD

```plang
CreateRecord
- insert into table, field1=%value1%, field2=%value2%, write to %id%

ReadRecord  
- select * from table where id=%id%, return 1, write to %record%

UpdateRecord
- update table set field=%newValue% where id=%id%

DeleteRecord
- delete from table where id=%id%
```

### API Integration

```plang
CallExternalApi
- post %Settings.ApiUrl%/endpoint
    headers:
        "X-API-key": "%Settings.ApiKey%"
    data: {
        "field": "%value%"
    }
    on error call HandleApiError
    write to %result%
```

### Form Processing

```plang
HandleFormSubmission
- validate %request.body.email% is not empty, "Email required"
- validate %request.body.amount% is not empty and larger than 0
- insert into submissions, email=%request.body.email%, amount=%request.body.amount%
- [ui] render "success.html", navigate
```

### File Processing

```plang
ImportCSV
- read data.csv, first row is header, write to %rows%
- foreach %rows% call ProcessRow item=%row%

ProcessRow
- insert into imported_data, column1=%row.header1%, column2=%row.header2%
```

## Debugging Plang Code

### Common Issues

**Variable not found:**
- Ensure variables are written to before reading: `write to %varName%`
- Check variable scope (variables from sub-goals need to be returned)

**Database errors:**
- Verify table exists and columns match
- Check data types match column definitions
- Ensure foreign key relationships are valid

**Goal not found:**
- Check file path is correct (relative paths supported)
- Verify goal name matches filename (case-sensitive)

**HTTP errors:**
- Add error handlers: `on error status code = 404, call HandleNotFound`
- Check API authentication headers
- Verify URL and request body format

### Debug Output

```plang
- write out "Debug: %variableName%"
- write out to system "System log: %value%"
- write out to user log "User-visible message"
```

## Architecture Patterns

### Microservice-Style Goals

Organize by feature domain:
```
/user/
  Create.goal
  Login.goal
  Update.goal
/order/
  Create.goal
  Process.goal
  Cancel.goal
/payment/
  Charge.goal
  Refund.goal
```

### Event-Driven Pattern

```plang
Events
- before each goal(including private) in /admin/.*, call CheckAdmin
- on app error, call goal HandleError
- before each goal in /api/.*, call RateLimitCheck
```

### Multi-Datasource Pattern

```plang
Setup
- create datasource "main"
- create datasource "analytics"
- create datasource "users/%userId%"

Query
- select * from orders o join analysis a on a.orderId=o.id
    datasource: "main", "analytics"
    write to %results%
```

## Reference Files

This skill includes comprehensive reference documentation:

### references/syntax.md
Complete syntax reference covering:
- Variable handling and operations
- Database operations (SELECT, INSERT, UPDATE, DELETE)
- HTTP operations (GET, POST with error handling)
- File operations (read, write, CSV, Excel, JSON)
- Control flow (if/then, loops, foreach)
- UI operations (rendering, DOM manipulation)
- LLM integration
- Events and goal control

### references/patterns.md
Proven implementation patterns:
- Database patterns (transactions, upsert, pagination)
- API integration patterns (auth, retry, pagination)
- Error handling patterns
- Payment processing patterns
- UI patterns (forms, modals, progressive enhancement)
- LLM processing patterns
- File processing patterns
- Authentication patterns
- Email patterns
- Testing patterns

### references/database.md
Database schema patterns:
- Table creation syntax
- Index patterns
- Column modifications
- Common schemas (users, orders, transactions, analytics)
- Data integrity patterns
- Multi-database queries

## Best Practices

1. **Read references first**: Before generating code, read the relevant reference file
2. **Use transactions**: For multi-step database operations, wrap in transactions
3. **Validate inputs**: Always validate user input and required parameters
4. **Handle errors**: Add error handlers for external calls (APIs, file operations)
5. **Use meaningful names**: Goal names and variables should be self-documenting
6. **Break down complexity**: Split complex logic into multiple goals
7. **Leverage caching**: Use cache for expensive operations (set cache, get cache)
8. **Test incrementally**: Build and test goals one at a time

## Example: Complete Feature Implementation

Here's a complete example showing best practices:

```plang
CreateOrder
- validate %user.id% is not empty, "User must be logged in"
- validate %cart% is not empty, "Cart cannot be empty"
- begin transaction "users/%user.id%"
- insert into orders, userId=%user.id%, status='pending', 
    amount=%cartTotal%, created=%now%
    write to %orderId%
- foreach %cart% call CreateOrderItem item=%cartItem%
- call goal ProcessPayment
    on error call HandlePaymentError
- update orders set status='paid' where id=%orderId%
- end transaction
- call goal SendOrderConfirmation
- [ui] render "order_success.html", navigate

CreateOrderItem
- insert into orderItems, orderId=%orderId%, 
    productId=%cartItem.productId%, 
    quantity=%cartItem.quantity%,
    price=%cartItem.price%
```

This example demonstrates:
- Input validation
- Transaction management
- Error handling
- Goal composition
- Database operations
- UI rendering
