# StockSense - feature checklist and known limitations

Legend: **Implemented** = code written. **Verified** = actually executed/tested. In the environment where this project was
written there was no .NET SDK, no MySQL and no internet package access, so **nothing below is marked Verified**.
Please run the test list in the README / your own checks.

## Implemented (code written, not executed)

### Dashboard
- Total products, low-stock count, expiring-soon / expired counts, today's sales, outstanding customer credit,
  this month's sales and gross profit, recent transactions (sales, purchases, payments), top alerts,
  monthly sales + gross profit chart (6 months) and 14-day sales chart. All numbers come from the database;
  empty database shows a welcome / set-up card.

### Products & categories
- Create, read, update, delete; unique SKU; category, unit, cost, price, stock, minimum stock, optional expiry, timestamps.
- Search, category filter, status filter (low / out / expiring / expired), sorting, pagination, details page with
  lifetime sales, margin, recent sales and purchases, stock correction (stock-take).
- Deletion blocked when a product appears in sales or purchases. Categories with products cannot be deleted.

### Stock & expiry
- Low stock, out of stock, expiring soon, expired, with severity (Critical / Warning) and recommended action.
- Expiry warning period configurable (Settings). Expired products cannot be sold (checked in the form list, in the
  service, and again inside the database UPDATE).
- Reorder suggestions (up to 2x minimum level) with estimated cost and last supplier.

### Suppliers & purchases
- Supplier CRUD with purchase history. Purchase with multiple lines, automatic totals, history, details page.
- Stock increases only when the purchase is saved (single database transaction). Product purchase cost is updated to the latest cost.
- One-time form token prevents duplicate purchases.

### Sales
- Multi-line sale, line totals, subtotal, discount, final amount (live preview + server recalculation).
- Paid in full (cash / UPI / card / other), credit (udhaar) with optional part payment, walk-in customers.
- Stock check, atomic stock decrease, no negative stock, duplicate-submission protection, database transaction.
- Selling price and purchase cost are saved on every sale line. Searchable history, printable invoice, quick "new customer" pop-up.

### Customers & udhaar
- Customer CRUD (name, 10-digit unique mobile, optional address), ledger page, customers with balance, part payments,
  payment history, validation (> 0, not more than outstanding), duplicate protection.
- Balances are always calculated from saved sales and payments.

### Reports
- Daily / weekly / monthly grouping with date filters and presets, revenue, COGS, gross profit and margin,
  revenue vs cash received vs credit, product-wise gross profit, best sellers, transaction list, Chart.js chart.
- Clearly states that gross profit is not net profit (expenses are not tracked).

### UI / platform
- Emerald + mint + charcoal theme, responsive sidebar (off-canvas on mobile), Bootstrap 5, Bootstrap Icons, confirm dialogs,
  empty states, flash messages, 404 / error pages, print styles.
- EF Core + Pomelo MySQL, DbContext with entity configurations, services, view models, DI, async queries.
- Demo data loader / remover (records flagged `IsDemo`, shown with a "Demo" badge).

## NOT tested (needs you to run)
- `dotnet restore` / `dotnet build` (package versions were chosen from knowledge, not checked against NuGet here).
- Any connection to MySQL, table creation, EF query translation, `ExecuteUpdate` / `ExecuteDelete` on your MySQL version.
- All pages in a browser, JavaScript behaviour, Chart.js charts, responsive layouts at mobile / desktop widths.
- Product CRUD, purchase stock increase, sale stock decrease, insufficient-stock rejection, expiry and low-stock alerts,
  credit sales and part payments, profit calculations, data persistence after restart.
- EF Core **migrations were not generated** (needs the SDK). The app creates the schema with `EnsureCreated` unless you
  generate migrations (README section 6).

## Known limitations
- One expiry date per product (no batch tracking). A purchase with a batch expiry replaces the product's expiry date.
- No sale cancellation, returns, or purchase editing/cancellation; sales cannot be edited after saving.
- No expenses, so only gross profit is reported. No taxes/GST. Single currency (INR).
- No login / user roles (local demo mode). Do not expose it to the internet.
- Stock correction changes the number without a history record.
- Bootstrap, icons, Chart.js, jQuery and fonts load from CDNs, so the browser needs internet access.
- Client-side script on the sale/purchase forms is required to add rows.
