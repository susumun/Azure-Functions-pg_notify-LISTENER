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
 1. LISTENER for pg_notify()
   1.CREATE Function App
   2.CREATE Timer Trigger Function 1
   3.add function.proj
   4.Copy Code 3.1
   5.INSERT NEW Data and Confirm the result of LISTENER Function

 2. Tracking By WwaterMark
   1.CREATE TABLE logs_watermark, logs_exec_history
   2.CREATE Timer Trigger Function 2
   3.add functin.proj
   4.CREATE BLOB CONTAINER
   5.bind in Blob and out Blob
   6.Copy Code 3.2













