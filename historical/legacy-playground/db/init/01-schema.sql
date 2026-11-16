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
