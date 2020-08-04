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





