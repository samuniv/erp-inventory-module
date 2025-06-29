-- Initialize PostgreSQL databases for Auth and Order services

-- Create databases
CREATE DATABASE erp_auth;
CREATE DATABASE erp_orders;

-- Create users (if they don't exist)
DO
$$
BEGIN
   IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'auth_user') THEN
      CREATE USER auth_user WITH PASSWORD 'auth123';
   END IF;
   
   IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'order_user') THEN
      CREATE USER order_user WITH PASSWORD 'order123';
   END IF;
END
$$;

-- Grant privileges
GRANT ALL PRIVILEGES ON DATABASE erp_auth TO auth_user;
GRANT ALL PRIVILEGES ON DATABASE erp_orders TO order_user;

-- Connect to each database and grant schema privileges
\c erp_auth;
GRANT ALL ON SCHEMA public TO auth_user;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO auth_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO auth_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO auth_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO auth_user;

\c erp_orders;
GRANT ALL ON SCHEMA public TO order_user;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO order_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO order_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO order_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO order_user;
