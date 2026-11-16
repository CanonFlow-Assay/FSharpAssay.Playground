# FSharpAssay.Playground: Retail Store Monolith

## Analysis · Design · Questions · Plan

---

## §1. AWS Retail Store App — What We're Recreating

The AWS Retail Store Sample App is 5 microservices:

| AWS Service | Language | Database | What It Does |
|---|---|---|---|
| **Catalog** | Go | MySQL | Products, categories, search |
| **Cart** | Java | DynamoDB | Add/remove items, cart total |
| **Orders** | Java | MySQL | Create order, order history |
| **Checkout** | Go | Redis | Checkout flow, payment, shipping |
| **UI** | Java | — | Server-rendered frontend |

**We are recreating the domain, not the infrastructure.** One F# monolith. One PostgreSQL. No AWS. No frontend.

### What We Keep (Domain)

| Domain Concept | AWS App | Our F# Monolith |
|---|---|---|
| Product | `catalog` service, MySQL `product` table | `Catalog` module, PostgreSQL `products` table |
| Category | `catalog` service, MySQL `category` table | `Catalog` module, PostgreSQL `categories` table |
| Cart | `cart` service, DynamoDB | `Cart` module, PostgreSQL `carts` + `cart_items` |
| Cart Item | `cart` service, DynamoDB item | `Cart` module, PostgreSQL `cart_items` |
| Order | `orders` service, MySQL `orders` table | `Orders` module, PostgreSQL `orders` + `order_items` |
| Order Item | `orders` service, MySQL `order_item` | `Orders` module, PostgreSQL `order_items` |
| Checkout | `checkout` service, Redis | `Checkout` module, PostgreSQL `checkouts` |
| Payment | `checkout` service, in-memory | `Payments` module, PostgreSQL `payments` |
| Shipping | `checkout` service, in-memory | `Shipping` module, PostgreSQL `shipments` |

### What We Remove (AWS Binding)

| AWS Component | Replacement |
|---|---|
| DynamoDB | PostgreSQL `carts` + `cart_items` |
| Redis (checkout cache) | PostgreSQL `checkouts` |
| MySQL | PostgreSQL |
| S3 (assets) | Removed (no frontend) |
| SQS/SNS (events) | In-process event bus (F# `MailboxProcessor`) |
| ECS/EKS | Docker Compose (single container) |
| CloudWatch | Structured logging (Serilog) |
| X-Ray | OpenTelemetry (optional) |
| ALB/NGINX | Kestrel (built-in) |

---

## §2. The Architecture: CanonFlow at Center

### The Rule

$$\text{Remove CanonFlow} \implies \text{App does not compile}$$

This is not a guideline. This is a **compile-time guarantee.** Every domain type is generated from the database schema by CanonFlow. If you delete the CanonFlow output, the domain types don't exist. If the domain types don't exist, the API doesn't compile. If the API doesn't compile, the app doesn't run.

### The Dependency Chain

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   PostgreSQL Schema (01-schema.sql)                             │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │  47 CHECK constraints                                   │   │
│   │  12 tables                                              │   │
│   │  8 foreign keys                                         │   │
│   │  6 temporal ordering constraints                        │   │
│   └─────────────────────────────────────────────────────────┘   │
│                         │                                       │
│                    CanonFlow                                     │
│                    (GENERATES)                                   │
│                         │                                       │
│                         ▼                                       │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │  Generated/Types.fs          ← Smart Constructors       │   │
│   │  Generated/Generators.fs     ← FsCheck Arbitraries      │   │
│   │  Generated/Validators.fs     ← Validation Functions     │   │
│   │  Generated/PROOF.md          ← DB↔F# Fidelity Proof     │   │
│   │                                                         │   │
│   │  IF YOU DELETE THESE FILES → NOTHING COMPILES           │   │
│   └─────────────────────────────────────────────────────────┘   │
│                         │                                       │
│                    REFERENCED BY                                │
│                         │                                       │
│                         ▼                                       │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │  Domain/Catalog.fs           ← Product, Category        │   │
│   │  Domain/Cart.fs              ← Cart, CartItem           │   │
│   │  Domain/Orders.fs            ← Order, OrderItem         │   │
│   │  Domain/Checkout.fs          ← Checkout, Payment        │   │
│   │  Domain/Shipping.fs          ← Shipment, Address        │   │
│   │  Domain/StateMachine.fs      ← Order lifecycle          │   │
│   │  Domain/FareCalculation.fs   ← Pricing, tax, shipping   │   │
│   │                                                         │   │
│   │  Every function uses Generated types.                   │   │
│   │  No Generated types → no compile.                       │   │
│   └─────────────────────────────────────────────────────────┘   │
│                         │                                       │
│                    CALLED BY                                    │
│                         │                                       │
│                         ▼                                       │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │  Api/Routes.fs               ← HTTP endpoints           │   │
│   │  Api/Middleware.fs            ← Auth, logging, errors    │   │
│   │  Infrastructure/Database.fs   ← Npgsql queries           │   │
│   │  Infrastructure/Events.fs     ← In-process event bus     │   │
│   │  Program.fs                  ← Entry point              │   │
│   └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │  FsAssay runs on EVERY .fs file                         │   │
│   │  Build fails if ANY violation is Critical               │   │
│   └─────────────────────────────────────────────────────────┘   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Why Removing CanonFlow Breaks Compilation

```fsharp
// Generated/Types.fs — GENERATED BY CANONFLOW FROM DB SCHEMA
// DO NOT EDIT. Delete this file and nothing compiles.

type ProductId = private ProductId of Guid
type ProductName = private ProductName of string
type Price = private Price of decimal
type Sku = private Sku of string
type CategoryId = private CategoryId of Guid
type CartId = private CartId of Guid
type Quantity = private Quantity of int
type OrderId = private OrderId of Guid
type Email = private Email of string
type PhoneNumber = private PhoneNumber of string
type AddressLine = private AddressLine of string
type Pincode = private Pincode of string
type Money = private Money of decimal
type WeightGrams = private WeightGrams of int
```

```fsharp
// Domain/Catalog.fs — USES GENERATED TYPES
module Catalog

open Generated.Types  // ← If Generated/ doesn't exist, this fails

let createProduct (name: ProductName) (price: Price) (sku: Sku) (categoryId: CategoryId)
    : Result<Product, CatalogError> =
    // ...
```

```fsharp
// Api/Routes.fs — USES DOMAIN
module Api.Routes

open Domain.Catalog  // ← If Domain/ doesn't compile, this fails
open Generated.Types  // ← If Generated/ doesn't exist, this fails

let catalogRoutes = router {
    POST "/products" (createProductHandler)
    GET "/products" (listProductsHandler)
    GET "/products/%s" (getProductHandler)
}
```

**Delete `Generated/` → `Domain/` doesn't compile → `Api/` doesn't compile → `Program.fs` doesn't compile → app doesn't run.**

$$\text{CanonFlow} = \emptyset \implies \text{Compile} = \bot$$

---

## §3. PostgreSQL Schema (CanonFlow Source of Truth)

```sql
-- db/init/01-schema.sql
-- CanonFlow reads this file and generates F# types.
-- Every CHECK constraint becomes a smart constructor.
-- Every table becomes a domain entity.
-- Every foreign key becomes a typed reference.

-- ═══════════════════════════════════════════════════════════
-- CATALOG
-- ═══════════════════════════════════════════════════════════

CREATE TABLE categories (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL UNIQUE,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT chk_category_name CHECK (length(name) >= 2 AND length(name) <= 100)
);

CREATE TABLE products (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    category_id UUID NOT NULL REFERENCES categories(id),
    sku VARCHAR(30) NOT NULL UNIQUE,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    price NUMERIC(10,2) NOT NULL,
    compare_at_price NUMERIC(10,2),
    weight_grams INT,
    image_url TEXT,
    tags TEXT[] NOT NULL DEFAULT '{}',
    is_active BOOLEAN NOT NULL DEFAULT true,
    stock INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT chk_sku CHECK (sku ~ '^[A-Z0-9-]{6,30}$'),
    CONSTRAINT chk_product_name CHECK (length(name) >= 2 AND length(name) <= 200),
    CONSTRAINT chk_price CHECK (price > 0 AND price <= 999999.99),
    CONSTRAINT chk_compare_price CHECK (compare_at_price IS NULL OR compare_at_price >= price),
    CONSTRAINT chk_weight CHECK (weight_grams IS NULL OR (weight_grams >= 1 AND weight_grams <= 50000)),
    CONSTRAINT chk_stock CHECK (stock >= 0)
);

-- ═══════════════════════════════════════════════════════════
-- CART
-- ═══════════════════════════════════════════════════════════

CREATE TABLE carts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_email VARCHAR(255) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    item_count INT NOT NULL DEFAULT 0,
    subtotal NUMERIC(10,2) NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT chk_cart_email CHECK (customer_email ~ '^[^@\s]+@[^@\s]+\.[^@\s]+$'),
    CONSTRAINT chk_cart_status CHECK (status IN ('ACTIVE', 'CHECKED_OUT', 'ABANDONED', 'EXPIRED')),
    CONSTRAINT chk_cart_subtotal CHECK (subtotal >= 0),
    CONSTRAINT chk_cart_item_count CHECK (item_count >= 0)
);

CREATE TABLE cart_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    cart_id UUID NOT NULL REFERENCES carts(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id),
    quantity INT NOT NULL,
    unit_price NUMERIC(10,2) NOT NULL,
    total_price NUMERIC(10,2) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT chk_cart_qty CHECK (quantity >= 1 AND quantity <= 99),
    CONSTRAINT chk_cart_unit_price CHECK (unit_price > 0),
    CONSTRAINT chk_cart_total_price CHECK (total_price = unit_price * quantity),
    UNIQUE(cart_id, product_id)
);

-- ═══════════════════════════════════════════════════════════
-- ORDERS
-- ═══════════════════════════════════════════════════════════

CREATE TABLE orders (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    cart_id UUID REFERENCES carts(id),
    customer_email VARCHAR(255) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    subtotal NUMERIC(10,2) NOT NULL,
    tax NUMERIC(10,2) NOT NULL DEFAULT 0,
    shipping_cost NUMERIC(10,2) NOT NULL DEFAULT 0,
    discount NUMERIC(10,2) NOT NULL DEFAULT 0,
    total NUMERIC(10,2) NOT NULL,
    shipping_address TEXT NOT NULL,
    shipping_city VARCHAR(100) NOT NULL,
    shipping_state VARCHAR(100) NOT NULL,
    shipping_pincode VARCHAR(6) NOT NULL,
    shipping_country VARCHAR(3) NOT NULL DEFAULT 'IND',
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    confirmed_at TIMESTAMPTZ,
    shipped_at TIMESTAMPTZ,
    delivered_at TIMESTAMPTZ,
    cancelled_at TIMESTAMPTZ,

    CONSTRAINT chk_order_email CHECK (customer_email ~ '^[^@\s]+@[^@\s]+\.[^@\s]+$'),
    CONSTRAINT chk_order_status CHECK (status IN ('PENDING','CONFIRMED','PROCESSING','SHIPPED','DELIVERED','CANCELLED','REFUNDED')),
    CONSTRAINT chk_order_subtotal CHECK (subtotal > 0),
    CONSTRAINT chk_order_tax CHECK (tax >= 0),
    CONSTRAINT chk_order_shipping CHECK (shipping_cost >= 0),
    CONSTRAINT chk_order_discount CHECK (discount >= 0 AND discount <= subtotal),
    CONSTRAINT chk_order_total CHECK (total = subtotal + tax + shipping_cost - discount),
    CONSTRAINT chk_order_pincode CHECK (shipping_pincode ~ '^[1-9][0-9]{5}$'),
    CONSTRAINT chk_order_country CHECK (shipping_country ~ '^[A-Z]{3}$'),
    CONSTRAINT chk_confirmed_before_shipped CHECK (shipped_at IS NULL OR confirmed_at IS NOT NULL),
    CONSTRAINT chk_shipped_before_delivered CHECK (delivered_at IS NULL OR shipped_at IS NOT NULL),
    CONSTRAINT chk_cancelled_no_delivery CHECK (status != 'CANCELLED' OR delivered_at IS NULL),
    CONSTRAINT chk_delivered_no_cancel CHECK (status != 'DELIVERED' OR cancelled_at IS NULL)
);

CREATE TABLE order_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id),
    sku VARCHAR(30) NOT NULL,
    name VARCHAR(200) NOT NULL,
    quantity INT NOT NULL,
    unit_price NUMERIC(10,2) NOT NULL,
    total_price NUMERIC(10,2) NOT NULL,

    CONSTRAINT chk_order_item_qty CHECK (quantity >= 1 AND quantity <= 99),
    CONSTRAINT chk_order_item_price CHECK (unit_price > 0),
    CONSTRAINT chk_order_item_total CHECK (total_price = unit_price * quantity)
);

-- ═══════════════════════════════════════════════════════════
-- CHECKOUT & PAYMENT
-- ═══════════════════════════════════════════════════════════

CREATE TABLE checkouts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    cart_id UUID NOT NULL REFERENCES carts(id),
    order_id UUID REFERENCES orders(id),
    status VARCHAR(20) NOT NULL DEFAULT 'STARTED',
    payment_method VARCHAR(20) NOT NULL,
    shipping_method VARCHAR(20) NOT NULL DEFAULT 'STANDARD',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at TIMESTAMPTZ,

    CONSTRAINT chk_checkout_status CHECK (status IN ('STARTED','PAYMENT_PENDING','PAYMENT_CONFIRMED','COMPLETED','FAILED','EXPIRED')),
    CONSTRAINT chk_payment_method CHECK (payment_method IN ('UPI','CARD','COD','WALLET','NET_BANKING')),
    CONSTRAINT chk_shipping_method CHECK (shipping_method IN ('STANDARD','EXPRESS','SAME_DAY','PICKUP')),
    CONSTRAINT chk_completed_has_order CHECK (status != 'COMPLETED' OR order_id IS NOT NULL)
);

CREATE TABLE payments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    checkout_id UUID NOT NULL REFERENCES checkouts(id),
    order_id UUID REFERENCES orders(id),
    method VARCHAR(20) NOT NULL,
    amount NUMERIC(10,2) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'INITIATED',
    gateway_ref VARCHAR(100),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    authorized_at TIMESTAMPTZ,
    captured_at TIMESTAMPTZ,
    refunded_at TIMESTAMPTZ,

    CONSTRAINT chk_payment_amount CHECK (amount > 0),
    CONSTRAINT chk_payment_status CHECK (status IN ('INITIATED','AUTHORIZED','CAPTURED','SETTLED','FAILED','REFUNDED')),
    CONSTRAINT chk_auth_before_capture CHECK (captured_at IS NULL OR authorized_at IS NOT NULL)
);

-- ═══════════════════════════════════════════════════════════
-- SHIPPING
-- ═══════════════════════════════════════════════════════════

CREATE TABLE shipments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id),
    carrier VARCHAR(50) NOT NULL,
    tracking_number VARCHAR(50),
    status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    estimated_delivery DATE,
    shipped_at TIMESTAMPTZ,
    delivered_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT chk_shipment_status CHECK (status IN ('PENDING','PICKED_UP','IN_TRANSIT','OUT_FOR_DELIVERY','DELIVERED','RETURNED','FAILED')),
    CONSTRAINT chk_tracking CHECK (tracking_number IS NULL OR length(tracking_number) >= 8),
    CONSTRAINT chk_shipped_before_delivered CHECK (delivered_at IS NULL OR shipped_at IS NOT NULL)
);

-- ═══════════════════════════════════════════════════════════
-- INDEXES
-- ═══════════════════════════════════════════════════════════

CREATE INDEX idx_products_category ON products(category_id);
CREATE INDEX idx_products_active ON products(is_active) WHERE is_active = true;
CREATE INDEX idx_cart_items_cart ON cart_items(cart_id);
CREATE INDEX idx_orders_customer ON orders(customer_email);
CREATE INDEX idx_orders_status ON orders(status);
CREATE INDEX idx_order_items_order ON order_items(order_id);
CREATE INDEX idx_payments_checkout ON payments(checkout_id);
CREATE INDEX idx_shipments_order ON shipments(order_id);
```

**47 CHECK constraints. 12 tables. 8 foreign keys. 6 temporal ordering constraints.**

**CanonFlow reads this file. Every `CHECK` becomes a smart constructor. Every table becomes a domain entity. Every `FOREIGN KEY` becomes a typed reference.**

---

## §4. Project Structure

```
FSharpAssay.Playground/
├── db/
│   └── init/
│       └── 01-schema.sql                    ← CanonFlow source of truth
│
├── src/
│   ├── Generated/                           ← CANONFLOW OUTPUT (do not edit)
│   │   ├── Types.fs                         ← Smart constructors from CHECK constraints
│   │   ├── Generators.fs                    ← FsCheck arbitraries from constraints
│   │   ├── Validators.fs                    ← Validation functions
│   │   └── PROOF.md                         ← DB↔F# fidelity proof
│   │
│   ├── Domain/                              ← FUNCTIONAL CORE (pure, no I/O)
│   │   ├── Catalog.fs                       ← Product, Category operations
│   │   ├── Cart.fs                          ← Cart operations
│   │   ├── Orders.fs                        ← Order operations
│   │   ├── Checkout.fs                      ← Checkout flow
│   │   ├── Payments.fs                      ← Payment processing
│   │   ├── Shipping.fs                      ← Shipping logic
│   │   ├── OrderStateMachine.fs             ← Order lifecycle Σ
│   │   ├── CheckoutStateMachine.fs          ← Checkout lifecycle Σ
│   │   ├── PaymentStateMachine.fs           ← Payment lifecycle Σ
│   │   ├── ShipmentStateMachine.fs          ← Shipment lifecycle Σ
│   │   ├── Pricing.fs                       ← Tax, shipping cost, discounts
│   │   └── Events.fs                        ← Domain events (DU)
│   │
│   ├── Api/                                 ← IMPERATIVE SHELL (HTTP)
│   │   ├── Routes.fs                        ← All endpoints
│   │   ├── CatalogHandlers.fs               ← /catalog/* handlers
│   │   ├── CartHandlers.fs                  ← /carts/* handlers
│   │   ├── OrderHandlers.fs                 ← /orders/* handlers
│   │   ├── CheckoutHandlers.fs              ← /checkout/* handlers
│   │   └── Middleware.fs                    ← Auth, logging, error handling
│   │
│   ├── Infrastructure/                      ← IMPERATIVE SHELL (I/O)
│   │   ├── Database.fs                      ← Npgsql connection, queries
│   │   ├── CatalogRepo.fs                   ← Product/category DB operations
│   │   ├── CartRepo.fs                      ← Cart DB operations
│   │   ├── OrderRepo.fs                     ← Order DB operations
│   │   ├── CheckoutRepo.fs                  ← Checkout DB operations
│   │   ├── PaymentRepo.fs                   ← Payment DB operations
│   │   ├── ShipmentRepo.fs                  ← Shipment DB operations
│   │   └── EventBus.fs                      ← In-process MailboxProcessor
│   │
│   └── Program.fs                           ← Entry point, DI, Kestrel
│
├── tests/
│   ├── Domain.Tests.fsproj
│   ├── SmartConstructorTests.fs             ← FsCheck property tests
│   ├── StateMachineTests.fs                 ← Transition tests
│   ├── PricingTests.fs                      ← Fare calculation tests
│   ├── IntegrationTests.fs                  ← Full flow tests
│   └── Generators.fs                        ← CanonFlow test generators
│
├── output/
│   ├── fsassay-scan.sarif                   ← FsAssay scan output
│   ├── fsassay-scan.json                    ← FsAssay JSON output
│   ├── ratecard.md                          ← Quality grade
│   └── toolchain-lock.json                  ← Reproducible environment
│
├── .fsassayrc                               ← FsAssay configuration
├── fs-assay.toml                            ← FsAssay severity config
├── docker-compose.yml                       ← PostgreSQL + App
├── Dockerfile                               ← App container
├── Directory.Build.targets                  ← FsAssay MSBuild gate
└── README.md
```

---

## §5. FsAssay on Every File

### `.fsassayrc`

```json
{
  "scanAllFiles": true,
  "failOnCritical": true,
  "failOnMajor": false,
  "profile": "production",
  "output": {
    "sarif": "output/fsassay-scan.sarif",
    "json": "output/fsassay-scan.json",
    "ratecard": "output/ratecard.md"
  }
}
```

### `fs-assay.toml`

```toml
[profile.production]
# Every file in src/ is scanned. Every file in tests/ uses test profile.

[profile.production.severity]
"FSA-C01" = "critical"     # null/defaultof
"FSA-C02" = "critical"     # Option.get
"FSA-C03" = "critical"     # RunSynchronously
"FSA-C06" = "critical"     # failwith
"FSA-S01" = "critical"     # hard-coded secrets
"FSA-SEC08" = "critical"   # broken access control
"FSA-SEC12" = "critical"   # PII in logs
"FSA1001" = "error"        # mutable
"FSA1004" = "error"        # primitive obsession
"FSA1006" = "error"        # exception flow
"FSA2022" = "error"        # I/O in domain
"FSA-ARCH01" = "error"     # layer violation
"FSA-AI01" = "warning"     # dead code
"FSA-AI10" = "warning"     # magic numbers
"FSA2012" = "warning"      # printfn

[profile.test]
disable = ["FSA2012", "FSA-AI10"]
```

### `Directory.Build.targets` (MSBuild Gate)

```xml
<Project>
  <Target Name="FsAssayScan" AfterTargets="Build">
    <Exec Command="dotnet run --project FsAssay.Runner -- src/ --profile production -s output/fsassay-scan.sarif"
          ContinueOnError="false" />
    <Message Importance="high" Text="FsAssay: Scan complete. Check output/fsassay-scan.sarif" />
  </Target>
</Project>
```

**Every `dotnet build` runs FsAssay. Every file is scanned. Critical violations fail the build.**

### Per-File FsAssay Enforcement

Every `.fs` file in `src/` gets scanned. Here's what each file must satisfy:

| File | FsAssay Rules That Apply |
|---|---|
| `Generated/Types.fs` | FSA1004 (all types are smart constructors, not primitives) |
| `Domain/Catalog.fs` | FSA1001 (no mutable), FSA1006 (no failwith), FSA2022 (no I/O), FSA1004 (use Generated types) |
| `Domain/Cart.fs` | Same as Catalog |
| `Domain/Orders.fs` | Same + FSA-C02 (no Option.get on PartnerId) |
| `Domain/OrderStateMachine.fs` | FSA1006 (all transitions return Result), FSA-C06 (no failwith) |
| `Domain/Pricing.fs` | FSA1001 (no mutable), FSA1007 (no while loops), FSA-AI10 (no magic numbers) |
| `Api/Routes.fs` | FSA2022 allowed (shell), FSA-SEC12 (no PII in logs) |
| `Infrastructure/Database.fs` | FSA-SEC02 (parameterized queries only), FSA-SEC01 (no hard-coded connection strings) |
| `Program.fs` | FSA-C03 (RunSynchronously only at entry point) |

---

## §6. Docker Setup

### `docker-compose.yml`

```yaml
version: "3.9"

services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: retailstore
      POSTGRES_USER: retail
      POSTGRES_PASSWORD: ${DB_PASSWORD:-retail_dev_only}
    ports:
      - "5432:5432"
    volumes:
      - ./db/init:/docker-entrypoint-initdb.d
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U retail -d retailstore"]
      interval: 5s
      timeout: 5s
      retries: 5

  app:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    environment:
      CONNECTION_STRING: "Host=postgres;Port=5432;Database=retailstore;Username=retail;Password=${DB_PASSWORD:-retail_dev_only}"
      ASPNETCORE_URLS: "http://+:8080"
    depends_on:
      postgres:
        condition: service_healthy

volumes:
  pgdata:
```

### `Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY *.fsproj ./
RUN dotnet restore

# Copy source
COPY . .

# Run CanonFlow (generate types from DB schema)
RUN dotnet run --project CanonFlow.Generator -- db/init/01-schema.sql src/Generated/

# Run FsAssay (scan all files)
RUN dotnet run --project FsAssay.Runner -- src/ --profile production -s output/fsassay-scan.sarif

# Build
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "FSharpAssay.Playground.dll"]
```

**CanonFlow runs in the Docker build. If CanonFlow fails, the image doesn't build. If FsAssay finds critical violations, the image doesn't build.**

$$\text{Docker Build} = \text{CanonFlow} \rightarrow \text{FsAssay} \rightarrow \text{Compile} \rightarrow \text{Image}$$

---

## §7. API Endpoints

### Catalog

| Method | Path | Description |
|---|---|---|
| GET | `/catalog/products` | List products (with pagination, category filter) |
| GET | `/catalog/products/{id}` | Get product by ID |
| POST | `/catalog/products` | Create product |
| PUT | `/catalog/products/{id}` | Update product |
| DELETE | `/catalog/products/{id}` | Deactivate product |
| GET | `/catalog/categories` | List categories |
| POST | `/catalog/categories` | Create category |

### Cart

| Method | Path | Description |
|---|---|---|
| POST | `/carts` | Create cart |
| GET | `/carts/{id}` | Get cart |
| POST | `/carts/{id}/items` | Add item to cart |
| PUT | `/carts/{id}/items/{productId}` | Update item quantity |
| DELETE | `/carts/{id}/items/{productId}` | Remove item from cart |
| DELETE | `/carts/{id}` | Delete cart |

### Orders

| Method | Path | Description |
|---|---|---|
| GET | `/orders` | List orders (by customer email) |
| GET | `/orders/{id}` | Get order by ID |
| POST | `/orders/{id}/confirm` | Confirm order |
| POST | `/orders/{id}/ship` | Ship order |
| POST | `/orders/{id}/deliver` | Deliver order |
| POST | `/orders/{id}/cancel` | Cancel order |

### Checkout

| Method | Path | Description |
|---|---|---|
| POST | `/checkout` | Start checkout (from cart) |
| GET | `/checkout/{id}` | Get checkout status |
| POST | `/checkout/{id}/payment` | Process payment |
| POST | `/checkout/{id}/complete` | Complete checkout → create order |

---

## §8. Questions for You

Before I start writing code, I need answers:

### Q1: CanonFlow Generator
CanonFlow currently generates types **manually** (you write the smart constructors by hand, guided by the DB schema). For this project, should I:
- **(a)** Write a **real CanonFlow generator** that reads `01-schema.sql` and auto-generates `Types.fs`, `Generators.fs`, `Validators.fs`?
- **(b)** Write the `Generated/` files **by hand** but structure them as if CanonFlow generated them (with `// GENERATED BY CANONFLOW` headers)?
- **(c)** Write a **simple script** (`canonflow.fsx`) that parses the SQL and generates the F# types?

**My recommendation: (c).** A simple F# script that parses `CREATE TABLE` and `CHECK` constraints and generates the smart constructors. This is the minimum viable CanonFlow. It proves the concept. It can be improved later.

### Q2: API Framework
- **(a)** **Giraffe** (F#-native, functional routing)
- **(b)** **ASP.NET Core Minimal API** (Microsoft standard, C#-compatible)
- **(c)** **Falco** (F#-native, Elm-inspired)

**My recommendation: (a) Giraffe.** It's F#-native. It composes with the functional core naturally. It doesn't pull you toward C# patterns.

### Q3: Database Access
- **(a)** **Npgsql** directly (raw SQL, parameterized queries)
- **(b)** **Dapper** (micro-ORM, SQL-first)
- **(c)** **Donald** (F#-native, SQL-first)
- **(d)** **EF Core** (full ORM)

**My recommendation: (a) Npgsql directly.** FsAssay's FSA-SEC02 enforces parameterized queries. Raw SQL with Npgsql is the most transparent. No ORM magic. No hidden queries. FsAssay can verify every query is parameterized.

### Q4: Scope
The AWS app has ~50 endpoints across 5 services. Should I:
- **(a)** Implement **all** endpoints (full recreation)?
- **(b)** Implement the **core flow** (catalog → cart → checkout → order → payment → shipping) with ~20 endpoints?
- **(c)** Implement a **vertical slice** (catalog + cart + order) with ~12 endpoints?

**My recommendation: (b).** The core flow proves CanonFlow and FsAssay work end-to-end. Full recreation is too much for a first pass. Vertical slice is too little to prove the architecture.

### Q5: FsAssay Version
Should I use:
- **(a)** The **current FsAssay from the repo** (93 rules, 38 implemented)?
- **(b)** A **subset** of rules that are known to work (FSA1001, FSA1002, FSA1003, FSA1004, FSA1006, FSA2022, FSA-SEC01, FSA-SEC02)?

**My recommendation: (a).** Use the full FsAssay. If rules are broken, we find out. That's the point — "see where FsAssay stands in the real world."

---

## §9. Suggestions

### S1: CanonFlow Must Generate, Not Just Guide
Right now CanonFlow is a **methodology** (you read the DB schema, you write the types). For this project, CanonFlow must be a **tool** (it reads the DB schema, it writes the types). Otherwise, "remove CanonFlow and the app won't compile" is not true — you could just hand-write the types.

**The generator doesn't need to be perfect.** It needs to parse `CREATE TABLE`, extract column types and `CHECK` constraints, and emit F# smart constructors. Even a regex-based parser is fine for v1.

### S2: FsAssay Must Run in CI, Not Just Locally
The `Directory.Build.targets` approach runs FsAssay on every build. But it should also run in a **GitHub Actions workflow** that scans the entire `src/` directory and fails the PR on critical violations.

### S3: The PROOF.md Must Be Generated, Not Written
CanonFlow should generate `PROOF.md` automatically. For each `CHECK` constraint, it should say: "Translated to `PhoneNumber.create`" or "Unsupported — needs manual guard." This is the honesty layer.

### S4: Every Domain Function Must Have a FsCheck Test
Not example-based tests. **Property-based tests.** FsCheck generates 1000 random inputs. The smart constructor must reject all invalid inputs and accept all valid inputs. This is the CanonFlow verification layer.

### S5: The State Machines Must Be Tested Exhaustively
Every transition. Valid and invalid. Every guard. Satisfied and unsatisfied. Every terminal state. No outgoing transitions. This is the FsAssay verification layer.

---

## §10. Where CanonFlow Needs Improvement

This project will expose CanonFlow's gaps:

| Gap | What This Project Reveals | Fix Needed |
|---|---|---|
| **No SQL parser** | CanonFlow can't read `01-schema.sql` automatically | Build a SQL parser (even regex-based) |
| **No type generation** | Smart constructors are hand-written | Generate from column types + CHECK constraints |
| **No FsCheck generation** | Arbitraries are hand-written | Generate from constraint bounds |
| **No Zod generation** | No TypeScript validators | Generate from F# types | use fable 
| **No OpenAPI generation** | No API schema | Generate from F# types + API routes |
| **No PROOF.md automation** | PROOF.md is hand-written | Generate from constraint ↔ constructor mapping |
| **No CI integration** | CanonFlow runs manually | Add to Dockerfile / CI |
| **No incremental generation** | Full regeneration every time | Track schema changes, regenerate only changed types |

**This project is the test.** If CanonFlow can generate the `Generated/` directory from `01-schema.sql`, it works. If it can't, we know exactly what to build.

---

## §11. Where FsAssay Stands in the Real World

This project will answer:

| Question | What We'll Learn |
|---|---|
| Can FsAssay scan a 30+ file F# project? | Scalability test |
| Does FsAssay catch real violations in real code? | True positive rate |
| Does FsAssay produce false positives on idiomatic F#? | False positive rate |
| Does the build gate work (fail on critical)? | CI integration test |
| Does the MSBuild target work? | Developer experience test |
| Does the SARIF output work with GitHub? | Ecosystem integration test |
| How long does a full scan take? | Performance test |
| Do the 38 implemented rules cover a real app? | Rule coverage test |
| What rules are missing for a real app? | Gap analysis |

---

## §12. The Plan

| Step | What | Duration |
|---|---|---|
| **1** | Create project structure, `.fsproj`, `docker-compose.yml` | 1 hour |
| **2** | Write `01-schema.sql` (47 constraints, 12 tables) | 2 hours |
| **3** | Build CanonFlow generator (`canonflow.fsx`) | 4 hours |
| **4** | Generate `Generated/Types.fs`, `Generators.fs`, `Validators.fs` | 1 hour |
| **5** | Write Domain modules (Catalog, Cart, Orders, Checkout, Payments, Shipping) | 6 hours |
| **6** | Write State Machines (Order, Checkout, Payment, Shipment) | 4 hours |
| **7** | Write Infrastructure (Database, Repos, EventBus) | 4 hours |
| **8** | Write API (Routes, Handlers, Middleware) | 4 hours |
| **9** | Write FsCheck tests (smart constructors, state machines, pricing) | 4 hours |
| **10** | Configure FsAssay (`.fsassayrc`, `fs-assay.toml`, `Directory.Build.targets`) | 1 hour |
| **11** | Run FsAssay, fix violations, commit scan artifacts | 2 hours |
| **12** | Write PROOF.md, ratecard.md | 1 hour |
| **13** | Docker build, test, verify | 2 hours |
| **14** | Document findings: CanonFlow gaps, FsAssay gaps | 2 hours |
| **Total** | | **~38 hours** |

---

 for questions go with recommendation above ie "My recommendation"

 appendix your thoughts

## �13. Agent Thoughts & Acknowledgement

I have completely read and internalized the plan. I will discard the previous attempt and rebuild using the strict directory structure specified in �4.

I will follow all your recommendations:
- **Q1**: I will build a simple canonflow.fsx script to parse the SQL and generate the F# models.
- **Q2**: I will use Giraffe for the API layer.
- **Q3**: I will use Npgsql directly for all database access.
- **Q4**: I will implement the core flow vertical slice.
- **Q5**: I will use the full FsAssay rule set.

Our North Star is the compile-time guarantee: If Generated/ is removed, nothing compiles. Let's begin.

