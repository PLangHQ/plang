# PLang for Small Business Owners

---

## Headline
**Run your operations without an IT department.**

---

## The Daily Grind

You're running a business, not a tech company — but somehow you're managing three different subscriptions just to track orders, inventory, and customers. The order system doesn't talk to the inventory sheet. The customer list lives in your email. And when a product runs low, you only find out when someone tries to buy it.

You've looked at "all-in-one" business software. It does 80% of what you need, charges $100/month, and the other 20% requires "contacting sales for a custom plan."

---

## The PLang Way

```plang
Start
- start webserver
- every day at 7am, call !CheckInventory

PlaceOrder - POST
- select stock from inventory where product=%request.product%, write to %item%
- if %item.stock% < 1 then
    - write out {error: 'Out of stock'}, status code 400
- insert into orders, customer=%request.customer%, product=%request.product%, quantity=%request.quantity%, date=%Now%, status='new', write to %orderId%
- update inventory set stock = stock - %request.quantity% where product=%request.product%
- send email to %request.customerEmail%, subject: "Order Confirmed #%orderId%", body: "Thanks for your order of %request.quantity%x %request.product%. We'll have it ready soon."
- write out {orderId: %orderId%, status: 'confirmed'}

CheckInventory
- select product, stock from inventory where stock < 10, write to %lowStock%
- if %lowStock.count% > 0 then
    - send email to owner@mybusiness.com, subject: "Low Stock Alert", body: "The following products are running low:\n%lowStock%"

AddCustomer - POST
- insert into customers, name=%request.name%, email=%request.email%, phone=%request.phone%, write to %id%
- write out {customerId: %id%, status: 'added'}

DailySales - GET
- select product, sum(quantity) as sold, count(*) as orders from orders where date >= %Today% group by product, write to %today%
- write out %today%
```

---

## Wait — that's the program?

That's your order management, inventory tracking, customer database, and daily sales report. Stock checks run every morning. Order confirmations go out automatically. Low stock alerts hit your inbox before you open the shop.

---

## What Just Happened

- **`start webserver`** — Your business app runs on port 8080. Take orders via API.
- **`insert into orders`** / **`update inventory`** — Database for orders and inventory created automatically. No setup.
- **`every day at 7am`** — Inventory checks run before you start your day.
- **`if stock < 10`** — Low stock alert sent automatically. Never be caught off-guard.
- **`send email`** — Order confirmations and alerts sent directly.
- **`DailySales - GET`** — Today's sales summary at a glance.

Need to change the low stock threshold? Change `10` to `20`. Need to add a "reorder" feature? Add a few more lines. Your software grows with your business.

---

## The Build / Run Split

PLang uses AI at **build time** to compile your English into executable instructions. At **runtime**, no AI — your business tools run reliably with zero ongoing cost. Build once for a few cents, run forever. No monthly subscription.

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir MyBusiness && cd MyBusiness
# Create Start.goal with your business operations
plang exec
```

Write how your business works in plain English. Build once. Run your operations.

[Full getting started guide →](/get-started)
