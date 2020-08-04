--CREATE PG FUNCTION
CREATE FUNCTION public.logs_notify()
    RETURNS trigger
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE NOT LEAKPROOF
AS $BODY$
DECLARE
  Id bigint;
BEGIN
  IF TG_OP = 'INSERT' OR TG_OP = 'UPDATE' THEN
	Id = NEW."id";
  ELSE
	Id = OLD."id";
  END IF;
  PERFORM pg_notify('logsnotification', TG_OP || ';' || Id );
  RETURN NEW;
END;
$BODY$;
