-- Una base de datos independiente por servicio (ver ESTANDAR-CALIDAD.md).
-- Se ejecuta una sola vez, al crear el volumen de datos de Postgres por
-- primera vez (docker-entrypoint-initdb.d).
CREATE DATABASE orders;
CREATE DATABASE inventory;
CREATE DATABASE payments;
CREATE DATABASE shipping;
