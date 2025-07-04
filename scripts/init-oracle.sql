-- Initialize Oracle database for Inventory and Supplier services

-- Connect as SYSDBA and create users
WHENEVER SQLERROR EXIT SQL.SQLCODE

-- Create Inventory user and tablespace
CREATE TABLESPACE INVENTORY_TBS
DATAFILE '/opt/oracle/oradata/XE/inventory_tbs.dbf'
SIZE 100M AUTOEXTEND ON;

CREATE USER INVENTORY IDENTIFIED BY "Oracle123!"
DEFAULT TABLESPACE INVENTORY_TBS
TEMPORARY TABLESPACE TEMP;

GRANT CONNECT, RESOURCE, DBA TO INVENTORY;
GRANT UNLIMITED TABLESPACE TO INVENTORY;

-- Create Supplier user and tablespace
CREATE TABLESPACE SUPPLIER_TBS
DATAFILE '/opt/oracle/oradata/XE/supplier_tbs.dbf'
SIZE 100M AUTOEXTEND ON;

CREATE USER SUPPLIER IDENTIFIED BY "Oracle123!"
DEFAULT TABLESPACE SUPPLIER_TBS
TEMPORARY TABLESPACE TEMP;

GRANT CONNECT, RESOURCE, DBA TO SUPPLIER;
GRANT UNLIMITED TABLESPACE TO SUPPLIER;

-- Enable common features
ALTER SESSION SET CONTAINER = CDB$ROOT;
GRANT CREATE SESSION TO INVENTORY;
GRANT CREATE SESSION TO SUPPLIER;

COMMIT;
EXIT;
