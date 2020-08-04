--CREATE PG FUNCTION
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
