# Azure-Functions-pg_notify-LISTENER

1. (PostgreSQL) CREATE PG FUNCTION

```
CREATE FUNCTION public."logs_update_notify"()
	RETURNS trigger
	LANGUAGE 'plpgsql'
	COST 100
	VOLATILE NOT LEAKPROOF 
AS $BODY$
DECLARE
  Id uuid;
BEGIN
  IF TG_OP = 'INSERT' OR TG_OP = 'UPDATE' THEN
	Id = NEW."Id";
  ELSE
	Id = OLD."Id";
  END IF;
  PERFORM pg_notify('logsnotification', TG_OP || ';' || Id );
  RETURN NEW;
END;
```
2. (PostgreSQL) CREATE PG TRIGGER

```
--CREATE TRIGGER (INSERT)
CREATE TRIGGER logs_notify_update
	AFTER INSERT 
	ON public."logs"
	FOR EACH ROW
	EXECUTE PROCEDURE public."logs_update_notify"();


--CREATE TRIGGER (UPDATE)
CREATE TRIGGER logs_notify_update
	AFTER UPDATE 
	ON public."logs"
	FOR EACH ROW
	EXECUTE PROCEDURE public."logs_update_notify"();


--CREATE TRIGGER (DELETE)
CREATE TRIGGER logs_notify_update
	AFTER DELETE 
	ON public."logs"
	FOR EACH ROW
	EXECUTE PROCEDURE public."logs_update_notify"();
```

3. (PostgreSQL) Confirm NOTYFY by pgsql listener command

4. (Azure) CREATE Functions(Dispatcher)
 - LISTENER for pg_notify()
   - CREATE Function App
   - CREATE Timer Trigger Function 1
   - add function.proj
   - Copy Code 3.1
   - INSERT NEW Data and Confirm the result of LISTENER Function

 - Tracking By WwaterMark
   - CREATE TABLE logs_watermark, logs_exec_history
   - CREATE Timer Trigger Function 2
   - add functin.proj
   - CREATE BLOB CONTAINER
   - bind in Blob and out Blob
   - Copy Code 3.2













