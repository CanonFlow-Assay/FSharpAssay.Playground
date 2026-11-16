-- Executed only in an owned, disposable database after the Webuy initial schema.
BEGIN;
CREATE FUNCTION pg_temp.reject(statement text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    BEGIN
        EXECUTE statement;
    EXCEPTION WHEN check_violation THEN RETURN;
    END;
    RAISE EXCEPTION 'expected check violation: %',statement;
END $$;
INSERT INTO dark_stores(id,name,lat,lng,address,pincode,city,fssai_license,gstin,cold_storage)
VALUES ('00000000-0000-0000-0000-000000000001','fixture',0,0,'fixture','600001','CHENNAI','12345678901234','22AAAAA0000A1Z5',false);
INSERT INTO products(id,sku,name,category,brand,mrp,selling_price,unit,storage_temp)
VALUES ('00000000-0000-0000-0000-000000000002','FIXTURE','fixture','DAIRY','fixture',10,10,'unit',0);
SELECT pg_temp.reject($s$INSERT INTO inventory(product_id,store_id) VALUES('00000000-0000-0000-0000-000000000002','00000000-0000-0000-0000-000000000001')$s$);
UPDATE dark_stores SET cold_storage=true;
INSERT INTO inventory(product_id,store_id,stock,reserved) VALUES('00000000-0000-0000-0000-000000000002','00000000-0000-0000-0000-000000000001',0,0);
SELECT pg_temp.reject($s$UPDATE inventory SET reserved=1$s$);
SELECT pg_temp.reject($s$UPDATE dark_stores SET cold_storage=false$s$);
SELECT pg_temp.reject($s$UPDATE products SET storage_temp=6$s$);
INSERT INTO consumers(id,phone,name) VALUES('00000000-0000-0000-0000-000000000003','9999999999','fixture');
INSERT INTO orders(id,ondc_txn_id,ondc_message_id,consumer_id,store_id,sub_total,total_fare,payment_method,otp,delivery_address,delivery_lat,delivery_lng,delivery_eta_minutes,distance_km,item_count)
VALUES('00000000-0000-0000-0000-000000000004',gen_random_uuid(),gen_random_uuid(),'00000000-0000-0000-0000-000000000003','00000000-0000-0000-0000-000000000001',100,100,'UPI','1234','fixture',0,0,30,2,1);
SELECT pg_temp.reject($s$UPDATE orders SET distance_km=2.1$s$);
SELECT pg_temp.reject($s$UPDATE dark_stores SET delivery_radius_km=1$s$);
SELECT pg_temp.reject($s$INSERT INTO ratings(order_id,rater_type,rater_id,rated_id,score) VALUES('00000000-0000-0000-0000-000000000004','CONSUMER',gen_random_uuid(),gen_random_uuid(),5)$s$);
UPDATE orders SET delivered_at=now(),confirmed_at=now(),packed_at=now(),picked_up_at=now(),in_transit_at=now(),status='DELIVERED';
SELECT pg_temp.reject($s$INSERT INTO ratings(order_id,rater_type,rater_id,rated_id,score,created_at) VALUES('00000000-0000-0000-0000-000000000004','CONSUMER',gen_random_uuid(),gen_random_uuid(),5,now()-interval '1 second')$s$);
SELECT pg_temp.reject($s$INSERT INTO ratings(order_id,rater_type,rater_id,rated_id,score,created_at) VALUES('00000000-0000-0000-0000-000000000004','CONSUMER',gen_random_uuid(),gen_random_uuid(),5,now()+interval '48 hours 1 second')$s$);
INSERT INTO ratings(order_id,rater_type,rater_id,rated_id,score,created_at) VALUES('00000000-0000-0000-0000-000000000004','CONSUMER',gen_random_uuid(),gen_random_uuid(),5,now()+interval '48 hours');
SELECT pg_temp.reject($s$UPDATE orders SET delivered_at=now()+interval '1 hour'$s$);
DO $$ BEGIN
    IF (SELECT count(*) FROM ratings)<>1 OR (SELECT count(*) FROM inventory)<>1 THEN
        RAISE EXCEPTION 'valid fixture rows missing';
    END IF;
END $$;
ROLLBACK;
