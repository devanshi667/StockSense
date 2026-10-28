-- StockSense: run this ONCE in MySQL Workbench / mysql CLI before starting the app.
-- It only creates the empty database. Tables are created by the application
-- (EF Core migrations, or automatically on first run). See README.md.

CREATE DATABASE IF NOT EXISTS stocksense
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

-- OPTIONAL: a dedicated user instead of root. Replace the password first!
-- CREATE USER IF NOT EXISTS 'stocksense'@'localhost' IDENTIFIED BY 'REPLACE_WITH_A_STRONG_PASSWORD';
-- GRANT ALL PRIVILEGES ON stocksense.* TO 'stocksense'@'localhost';
-- FLUSH PRIVILEGES;

-- To wipe EVERYTHING and start over (deletes all data!):
-- DROP DATABASE stocksense;
-- CREATE DATABASE stocksense CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
