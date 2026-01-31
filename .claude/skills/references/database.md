# Database Schema Patterns in Plang

## Table Creation Syntax

### Basic Table

```plang
- create table users, columns:
    email(string, not null, unique),
    name(string),
    created(datetime, now)
```

### Table with Foreign Keys

```plang
- create table orders, columns:
    userId(number, foreign key to users.id, not null),
    amount(number, not null),
    status(string),
    created(datetime, now)
```

### Table with Enums

```plang
- create table orders, columns:
    status(enum('pending', 'paid', 'failed'), default 'pending', not null),
    paymentType(enum('card', 'aur', 'netgiro'))
```

### Table with Computed Columns

```plang
- create table orderItems, columns:
    amount(number, not null),
    discount(real, not null, default 0),
    quantity(number, not null),
    total(computed amount*(1-discount)*quantity, round 0)
```

### Table with Constraints

```plang
- create table cart, columns:
    identityId(number, foreign key to identities.id, not null),
    userId(number, foreign key to users.id),
    variantId(number, not null),
    quantity(number, default 1),
    created(datetime, now, indexed)
    unique key on variantId and identityId
```

## Index Patterns

### Simple Index

```plang
- create index on column_name on table table_name
- create unique index on email column in emails table
```

### Composite Index

```plang
- create unique index on fromDate, toDate, contractId on table contractReports
```

### Conditional Index

```plang
- CREATE UNIQUE INDEX idx_cart_variant_user_notnull
    ON cart(variantId, userId)
    WHERE userId IS NOT NULL
```

### Named Index

```plang
- CREATE INDEX IF NOT EXISTS idx_visits_product_date
  ON visits(type, externalId, date)
```

## Column Modifications

### Add Columns

```plang
- add column to users, courseInfo(string)
- add columns to users, columns:
    terms(bool, default false),
    privacy(bool, default false),
    termsDate(datetime)
- add string columns "bisacLevel1", "bisacLevel2" to table "courses"
```

### Drop Columns

```plang
- drop column ignore from stepTimings
```

### Modify Columns

```plang
- modify table codes, columns status cannot be 'issued' if code column is null
```

## Common Schema Patterns

### User Authentication Schema

```plang
Setup
- create table users, columns:
    email(string, not null, unique),
    created(datetime, now),
    lastAccess(datetime, now),
    role(string, default '["customer"]', not null)
- create table identities, columns:
    identity(string, not null, unique),
    userId(foreign key to users.id, null),
    accessCode(string),
    accessCodeCreated(datetime),
    created(datetime, now)
```

### E-commerce Order Schema

```plang
Setup
- create table orders, columns:
    created(datetime, now),
    status(enum('pending', 'paid', 'failed')),
    paymentType(enum('card', 'aur', 'netgiro')),
    amount(number, not null),
    vat(real, not null),
    type(enum('debit', 'credit'), not null)
- create table orderItems, columns:
    orderId(foreign key to orders.id, not null, ON DELETE CASCADE),
    variantId(number, not null),
    sku(string, not null),
    amount(number, not null),
    discount(real, not null, default 0),
    quantity(number, not null),
    total(computed amount*(1-discount)*quantity, round 0),
    vat(real, not null)
```

### Transaction/Payment Schema

```plang
Setup
- create table cardTransactions, columns:
    userId(number, not null),
    orderId(number, not null),
    checkoutReference(string, not null),
    status(enum('pending', 'done', 'cancelled'), default 'pending', not null),
    created(datetime, now),
    response(string, not null),
    type(string)
- create unique index on cardTransactions, column: checkoutReference
```

### Error Logging Schema

```plang
Setup
- create table errors, columns:
    type(string),
    statusCode(number),
    message(string),
    error(string),
    memoryStack(string),
    identityId(number),
    userId(number),
    created(datetime, default now)
```

### Full-Text Search Schema

```plang
Setup
- CREATE VIRTUAL TABLE products_fts USING fts5(
    title, description,
    content='products',
    content_rowid='id',
    prefix='2 3 4 5 6 7 8 9 10'
)
```

### Analytics/Tracking Schema

```plang
Setup
- create table visits, columns:
    type(string, not null),
    slug(string, not null),
    externalId(number),
    date(string, not null),
    created(datetime, now),
    updated(datetime, now),
    count(number, 1)
    unique index on type, slug, date
- create table visitsIdentity, columns:
    identityId(string, not null),
    type(string, not null),
    slug(string, not null),
    externalId(number),
    created(datetime, now),
    updated(datetime, now)
```

### Audit Trail Schema

```plang
Setup
- create table history, columns:
    variantId(number, not null),
    new_price(real, not null),
    old_price(real),
    created(datetime, now)
- create table statusHistory, columns:
    variantId(number, not null),
    new_status(string, not null),
    old_status(string, not null),
    created(datetime, now)
```

## Data Source Patterns

### Main Application Database

```plang
Setup
- create datasource "data"
```

### User-Specific Database

```plang
SetupUser
- create datasource "/users/%user.id%"
```

### Analytics Database

```plang
Analytics
- create data source "analytics"
```

### Separate Service Database
Setup datasource without event sourcing

```plang
Setup
- create datasource "marketing", dont keep history
```

## Migration Patterns

### Execute SQL File

```plang
- execute "CREATE_factSales.sql", table list "factSales"
- execute sql file "CREATE_productVariants_new.sql", table:'productsVariants'
```

### Recreate Table Pattern

```plang
ReCreateTable
- execute sql 'PRAGMA foreign_keys = OFF'
- begin transaction
- CREATE TABLE table_new (...)
- INSERT INTO table_new SELECT * FROM table
- DROP TABLE table
- ALTER TABLE table_new RENAME TO table
- end transaction
- execute sql 'PRAGMA foreign_keys = ON'
```

### Add Column with Default

```plang
- ALTER TABLE products ADD COLUMN pop_score REAL NOT NULL DEFAULT 0
```

## Data Integrity Patterns

### Cascade Delete

```plang
- create table orderItems, columns:
    orderId(foreign key to orders.id, not null, ON DELETE CASCADE)
```

### Unique Constraints

```plang
- create table cart, columns:
    variantId(number, not null),
    identityId(number, not null)
    unique key on variantId and identityId
- orderId and userId create unique key
```

### Check Constraints

```plang
- CREATE TABLE contractBatches (
    status TEXT NOT NULL DEFAULT 'queued' CHECK(status IN ('queued','sent','accepted','failed'))
  )
```

## Multi-Database Query Pattern

```plang
- select fs.*, p.title from factSales fs
    join products p on p.id=fs.productId
    where fs.date > %date%
    ds: "analytics", "data"
    write to %results%
```

## Ignore Duplicate Pattern

```plang
- insert into currencies, symbol="GBP", price=5, ignore on duplicate(symbol)
- insert into segmentEmails (emailId, segmentId)
    select id,%segmentId% from emails
    ignore on contraint error
```

## Update on Duplicate Pattern

```plang
- upsert into stepTimings,
    stepText=%text%,
    prPath=%path%(prPath is unique index),
    %timeMs%(update this on duplicate prPath),
    updated=%now%(update this on duplicate prPath)
```
