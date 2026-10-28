# StockSense - Smart Grocery Shop Management System

ASP.NET Core MVC (.NET 8) + Entity Framework Core 8 + MySQL, with Bootstrap 5, Bootstrap Icons and Chart.js.
A college mini-project: products, stock & expiry alerts, suppliers & purchases, sales (cash / UPI / credit),
customer udhaar ledger, and sales / gross-profit reports. No login (local demo mode).

> «Project Status: StockSense is a college mini-project developed using ASP.NET Core MVC (.NET 8), Entity Framework Core 8, and MySQL. The application has been built and run in a local development environment. Core workflows should be verified using the testing checklist before submission. See "FEATURES.md" (FEATURES.md) for feature details and testing notes.»
---

## 1. Install the prerequisites (once)

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | **8.0** (9 or 10 also work: the app is allowed to roll forward) | https://dotnet.microsoft.com/download |
| MySQL Server | 8.0 or newer (MariaDB 10.6+ also works) | MySQL Workbench is optional |
| VS Code | with the *C# Dev Kit* extension | optional but recommended |
| Internet | needed to load Bootstrap, Chart.js, icons and fonts from CDNs | the app itself runs locally |

Check: open PowerShell and run `dotnet --version` and `mysql --version`.

## 2. Put the project in your folder

Extract the ZIP **into** `C:\5thsem .NET Project\StockSense` so that `StockSense.csproj` sits directly in that folder
(not in a sub-folder). Then open the folder in VS Code (`File > Open Folder`).

## 3. Create the empty database

Using MySQL Workbench or the command line, run the file `Database/create_database.sql`:

```powershell
mysql -u root -p < "Database/create_database.sql"
```

This only creates an empty database called `stocksense`. The tables are created by the app.

## 4. Configure the database password (never commit it)

1. Copy `appsettings.Example.json` to **`appsettings.Local.json`** (same folder).
2. Open `appsettings.Local.json` and replace `PUT_YOUR_MYSQL_PASSWORD_HERE` with **your own** MySQL password.
   If your MySQL user is not `root`, change `User=root` too.
3. If you use **MariaDB**, set `"Provider": "MariaDb"` and a matching `"ServerVersion"` (for example `10.11.0`).

`appsettings.Local.json` is listed in `.gitignore`, so your password stays out of Git. The committed
`appsettings.json` only contains the placeholder `CHANGE_ME`; if you start the app without configuring it you will
see a setup page explaining what to do.

## 5. Build and run

```powershell
cd "C:\5thsem .NET Project\StockSense"
dotnet restore
dotnet build
dotnet run
```

Open **http://localhost:5080** (the browser opens automatically). On first start the app creates all tables in the
`stocksense` database. Data is stored in MySQL, so it is still there after you stop and restart the app.

### Try it with sample data
Go to **Settings > Load demo data** (or click the button on the empty dashboard). Everything it creates is labelled
**Demo**. **Settings > Remove demo data** deletes only those records. Your own data is never touched.

## 6. Optional: use EF Core migrations

By default the app creates the schema directly from the model (`EnsureCreated`), which needs no extra command. If your
teacher wants migrations, generate them once:

```powershell
dotnet tool restore
dotnet ef migrations add InitialCreate
```

Start from an empty database for this (drop it first if the app already created tables:
`mysql -u root -p -e "DROP DATABASE stocksense; CREATE DATABASE stocksense CHARACTER SET utf8mb4;"`).
After the `Migrations` folder exists, the app applies them automatically on startup. You can also run
`dotnet ef database update` yourself. Later model changes: `dotnet ef migrations add <Name>`.

To start over with an empty database at any time, drop and re-create `stocksense` with `Database/create_database.sql`
(the last lines of that file show the commands).

## 7. Using the app (suggested order)

1. **Categories** - add e.g. Dairy, Grains.
2. **Products** - name, unique SKU, category, unit, purchase cost, selling price, minimum stock, optional expiry.
3. **Suppliers** then **Purchases** - recording a purchase *adds* stock and updates the product's cost.
4. **New sale** - choose products and quantities; pay in full, or put it on credit (udhaar) for a customer.
5. **Customers & udhaar** - open a customer's ledger and record (part) payments.
6. **Stock alerts** - low stock, expiring and expired items, with reorder suggestions.
7. **Reports** - daily / weekly / monthly revenue, cost of goods, gross profit, product-wise profit, best sellers.

## 8. How the numbers are calculated

* **Revenue** = bills raised in the period (cash and credit), after discounts.
* **Cost of goods sold** = purchase cost *saved on each sale line at the time of sale* (later cost changes do not rewrite history).
* **Gross profit** = revenue - cost of goods sold. This is **not** net profit: expenses are not tracked, so net profit is never shown.
* **Customer balance** = sum of (bill total - paid at sale) for their sales - sum of their saved payments. Never typed in by hand.
* **Cash received** = paid at time of sale + udhaar payments collected. Credit given is shown separately.
* **Low stock** = stock at or below the product's minimum level. **Expiring soon** = expires within the warning period
  (default 30 days, change it in Settings). **Expired** products cannot be sold.
* **Reorder suggestion** = enough to bring stock up to 2x the minimum level.

## 9. Project structure

```
StockSense.csproj          project file (net8.0, EF Core 8.0.11, Pomelo MySQL 8.0.2)
Program.cs                 start-up, DI, middleware
appsettings.json           safe defaults (placeholder password)
appsettings.Example.json   copy to appsettings.Local.json and add your password
Models/                    entities (Product, Sale, SaleItem, Customer, Payment, ...)
Data/                      DbContext, entity configurations, initializer, design-time factory
Services/                  business rules: SaleService, PurchaseService, CustomerLedgerService,
                           ReportService, AlertService, DashboardService, DemoDataService, ...
Controllers/               MVC controllers
ViewModels/                strongly typed view models
Views/                     Razor views (layout, dashboard, products, sales, ...)
ViewComponents/            sidebar alert badge
wwwroot/css, wwwroot/js    theme, charts, sale/purchase row editor
Database/create_database.sql
```

## 10. Safety features

* Antiforgery token required on every POST.
* Server-side validation on all forms (plus client-side validation).
* Stock is changed with one atomic `UPDATE ... WHERE stock >= quantity` inside a database transaction, so stock can
  never go negative, even with two cashiers at once. Purchases, sales and payments carry a one-time form token, so a
  double click or page refresh cannot save them twice.
* Products, customers, suppliers and categories that have history cannot be deleted.

## 11. Troubleshooting

| Problem | Fix |
|---------|-----|
| Page says *StockSense needs a database connection* | MySQL not running, wrong password/user in `appsettings.Local.json`, or the `stocksense` database does not exist. Fix and restart (`Ctrl+C`, `dotnet run`). |
| `Access denied for user 'root'` | Wrong password. Test with `mysql -u root -p`. |
| `Unable to connect to any of the specified MySQL hosts` | Start the MySQL service (Windows: *Services > MySQL80 > Start*). Check the port (default 3306). |
| `Unknown database 'stocksense'` | Run step 3. |
| `dotnet build` error mentioning a file and line | Read the message, fix the typo, or send it to your teacher/assistant together with the file. |
| NU1102 / package not found | You need internet access for `dotnet restore`; check proxy settings. |
| `You must install or update .NET` | Install the .NET 8 SDK/runtime. |
| Port 5080 already in use | Change `applicationUrl` in `Properties/launchSettings.json`. |
| Page has no styling / charts missing | The CDN files could not load - check your internet connection. |
| `Table ... already exists` after adding migrations | The app already created tables with `EnsureCreated`. Drop the database and start again (section 6). |
| Different MySQL version warnings | Set `Database:ServerVersion` to your real version (e.g. `8.4.0`). |
