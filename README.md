# Azure-Functions-pg_notify-LISTENER

0. Previous Step is here.
  - pgsql_hackathon
    - https://github.com/rioriost/pgsql_hackathon

1. (PostgreSQL) CREATE PG FUNCTION

    ```
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
    ```
2. (PostgreSQL) CREATE PG TRIGGER

    ```
    --CREATE TRIGGER (INSERT)
    CREATE TRIGGER logs_notify_insert
      AFTER INSERT 
      ON public."logs"
      FOR EACH ROW
      EXECUTE PROCEDURE public."logs_notify"();


    --CREATE TRIGGER (UPDATE)
    CREATE TRIGGER logs_notify_update
      AFTER UPDATE 
      ON public."logs"
      FOR EACH ROW
      EXECUTE PROCEDURE public."logs_notify"();


    --CREATE TRIGGER (DELETE)
    CREATE TRIGGER logs_notify_delete
      AFTER DELETE 
      ON public."logs"
      FOR EACH ROW
      EXECUTE PROCEDURE public."logs_notify"();
    ```

3. (PostgreSQL) Confirm NOTYFY by pgsql listener command

- Start Command Prompt
    ```
    psql -h [HOSTNAME] -U [USERNAME] -d [DBNAME] -p 5432
    ```

- LISTEN 
    ```
    --LISTEN [Channel Name];
    LISTEN logsnotification;
    ```

4. (Azure) CREATE Functions(Dispatcher) - LISTENER for pg_notify()
  - CREATE Function App
  - CREATE Timer Trigger Function 1
  - add function.proj
      ```
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
              <TargetFramework>netstandard2.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
              <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="3.1.4" />
              <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL.Design" Version="1.1.0" />
          </ItemGroup>
        </Project>
      ```
  - Copy to CSX Script(Please replace login info)
    ```
      using System;
      using System.Collections.Generic;
      using System.IO;
      using Npgsql;

      private static string Host = "HOST";
      private static string User = "USER@XXX";
      private static string DBname = "DBNAME";
      private static string Password = "PASS";
      private static string Port = "5432";
      private static string TriggerChannelName = "logsnotification";

      public static void Run(TimerInfo myTimer, ILogger log)
      {
          try{
              log.LogInformation($"===PG LISTENER=== Executed at: {DateTime.Now}");


              string connString = string.Format("Server={0};Username={1};Database={2};Port={3};Password={4};SSLMode=Prefer", Host, User, DBname, Port, Password);
              List<string> idList = new List<string>();


              using (var conn = new NpgsqlConnection(connString)){
                  conn.Open();

                  conn.Notification += ( sender, e ) =>
                  {
                      string message = $"Notify:{e.Channel}, AdditionalData={e.Payload}";
                      //log.LogInformation( message );
                      string[] str_Payload = e.Payload.Split(';');
                      //log.LogInformation($"str_Payload={string.Join(",",str_Payload)}");
                      if(str_Payload[0]=="INSERT")idList.Add(str_Payload[1]);
                      //log.LogInformation($"idList={string.Join(",",idList)}");
                  };

                  using (var command = new NpgsqlCommand("listen "+ TriggerChannelName +";", conn)) { 
                      command.ExecuteNonQuery(); 
                  }

                  if(idList.Count()==0)log.LogInformation($"===PG LISTENER=== : [RESULT]Inserted Rows = {idList.Count()}");
                  else log.LogInformation($"===PG LISTENER=== : [RESULT]Inserted Rows = {idList.Count()} : idList = {string.Join(",",idList)}");
              }
          }
          catch ( Exception ex )
          {
              log.LogInformation($"===PG LISTENER=== : COUGHT ERROR : {0}", ex.ToString());
          }
          finally
          {
              log.LogInformation($"===PG LISTENER=== : Excecution Completed at: {DateTime.Now}");
          }
      }
    ```
  - INSERT NEW Data or Add new file(dummy.data) in the folder associated with the BLOB trigger and Confirm the result of LISTENER Function log
    ```
      insert into logs(content) values('{"id":"test"}');
    ```

5. (Azure) CREATE Functions(Dispatcher) - Tracking By WwaterMark
    - CREATE TABLE logs_watermark, logs_exec_history
    ```
    CREATE TABLE public.logs_watermark
    (
        id bigint NOT NULL,
        status text COLLATE pg_catalog."default",
        CONSTRAINT logs_watermark_pkey PRIMARY KEY (id)
    );

    CREATE TABLE public.logs_exec_history
    (
        id_start bigint NOT NULL,
        id_end bigint NOT NULL,
        exec_time timestamp without time zone,
        CONSTRAINT logs_exec_history_pkey PRIMARY KEY (id_start, id_end)
    );
    ```
   - CREATE Timer Trigger Function 2
   - add functin.proj
   - CREATE BLOB CONTAINER
     - add Container for archive data
   - bind in Blob and out Blob
     - go to Integration tab
     - add Input and Output
     - Confirm function.json as below
    ```
    {
      "bindings": [
        {
          "name": "myTimer",
          "type": "timerTrigger",
          "direction": "in",
          "schedule": "0 */1 * * * *"
        },
        {
          "name": "inputBlob",
          "direction": "in",
          "type": "blob",
          "path": "archive/{datetime:yyyy}/{datetime:MM}/{datetime:dd}/{datetime:HH}/archive.json",
          "connection": "storagexxxblob_STORAGE"
        },
        {
          "name": "outputBlob",
          "path": "archive/{datetime:yyyy}/{datetime:MM}/{datetime:dd}/{datetime:HH}/archive.json",
          "connection": "storagexxxblob_STORAGE",
          "direction": "out",
          "type": "blob"
        }
      ]
    }
    ```

   - Copy to CSX Script(Please replace login info)
    ```
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Npgsql;
    using System.Net;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Host.Bindings.Runtime;

    private static string Host = "HOST";
    private static string User = "USER@XXX";
    private static string DBname = "DBNAME";
    private static string Password = "PASS";
    private static string Port = "5432";
    private static string TriggerChannelName = "logsnotification";
    private static string outputString = "";

    public static void Run(TimerInfo myTimer, in string inputBlob, out string outputBlob, ILogger log)
    {
        try{
            log.LogInformation($"TimerTrigger2(Watermark) executed at: {DateTime.Now}");

            string connString = string.Format("Server={0};Username={1};Database={2};Port={3};Password={4};SSLMode=Prefer", Host, User, DBname, Port, Password);
            string sql = "";
            long id_watermark=0;
            long latest_id_watermark=0;
            string exec_status ="";
            string exec_status_RUNNING ="running";
            string exec_status_FINISHED ="finished";
    
            using (var conn = new NpgsqlConnection(connString)){
                conn.Open();

                //Get Watermark
                sql = "SELECT id,status FROM logs_watermark;";
                using (var command = new NpgsqlCommand(sql, conn)) { 
                    using(var reader = command.ExecuteReader()){
                        while(reader.Read()){
                            id_watermark = (long)reader["id"];
                            exec_status = (string)reader["status"];
                            log.LogInformation($"---Current Status : {id_watermark} - {exec_status}");
                        }
                    }
                }

                //SKIP if Status IS Already Running
                if(exec_status == exec_status_FINISHED){
                    log.LogInformation($"---[Started]Distpaching : {id_watermark}");

                    //Update Status
                    sql = "UPDATE logs_watermark set status = '" + exec_status_RUNNING + "' WHERE id = " + id_watermark + ";";
                    using (var command = new NpgsqlCommand(sql, conn)) {
                        command.ExecuteNonQuery();  
                        log.LogInformation($"---Updated Execution Status : {id_watermark}");
                    }

                    //Get Watermark
                    sql = "SELECT id,status FROM logs_watermark;";
                    using (var command = new NpgsqlCommand(sql, conn)) { 
                        using(var reader = command.ExecuteReader()){
                            while(reader.Read()){
                                id_watermark = (long)reader["id"];
                                exec_status = (string)reader["status"];
                                log.LogInformation($"---Get Watermark : {id_watermark} - {exec_status}");
                            }
                        }
                    }

                    //Get Original Data & Dispatch Data
                    sql = "SELECT id, content FROM logs WHERE id > " + id_watermark +" ORDER BY id;";
                    latest_id_watermark = id_watermark;
                    
                    List<long> idList = new List<long>();
                    List<object> contentList = new List<object>();
                
                    using (var command = new NpgsqlCommand(sql, conn)) { 
                        using(var reader = command.ExecuteReader()){
                            while(reader.Read()){
                                latest_id_watermark = (long)reader["id"];
                                idList.Add((long)reader["id"]);
                                contentList.Add(reader["content"]);
                            }

                            log.LogInformation($"---Get Latest id watermark : {latest_id_watermark}");
                            log.LogInformation($"---# of idList.Count() : {idList.Count()}");
                            log.LogInformation($"---# of contentList.Count() : {contentList.Count()}");

                            for(int i = 0; i < idList.Count(); i++) {
                                outputString = outputString + "{\"timestamp\": \"" + DateTime.Now + "\", \"id\" : " + Convert.ToString(idList[i]) + ", \"content\" : " + contentList[i] + "}\n";
                            }
                        }
                    }

                    //Update Watermark
                    sql = "UPDATE logs_watermark set id = " + latest_id_watermark + ", status = '" + exec_status_FINISHED + "' WHERE id = " + id_watermark + ";";
                    using (var command = new NpgsqlCommand(sql, conn)) {
                        command.ExecuteNonQuery();  
                        log.LogInformation($"---Updated latest id watermark and status: {latest_id_watermark}");
                    }

                    if(id_watermark < latest_id_watermark){
                        //Update Exec Histoty
                        sql = "INSERT INTO logs_exec_history values (" + id_watermark + ", " + latest_id_watermark + ", NOW());";
                        using (var command = new NpgsqlCommand(sql, conn)) {
                            log.LogInformation($"---Updated log exec history: {id_watermark} to {latest_id_watermark}");
                            command.ExecuteNonQuery();  
                        }
                    }

                    //Get Latest Watermark
                    sql = "SELECT id,status FROM logs_watermark;";
                    using (var command = new NpgsqlCommand(sql, conn)) { 
                        using(var reader = command.ExecuteReader()){
                            while(reader.Read()){
                                id_watermark = (long)reader["id"];
                                exec_status = (string)reader["status"];
                                log.LogInformation($"---[Completed]Latest Watermark & Status : {id_watermark} - {exec_status}");
                            }
                        }
                    }
                }else if(exec_status==exec_status_RUNNING){
                    outputString = "{\"timestamp\": \"" + DateTime.Now + "\", \"id\" : \"error\", \"content\" : \"ALREADY RUNNING\"}\n";
                    log.LogInformation($"---[SKIP]ALREADY RUNNING : {exec_status} ---");
                }else{
                    outputString = "{\"timestamp\": \"" + DateTime.Now + "\", \"id\" : \"error\", \"content\" : \"ABEND\"}\n";
                    log.LogInformation($"---[ABEND]ERROR : {exec_status} ---");
                    
                }
            }
        }
        catch ( Exception ex )
        {
            log.LogInformation($"TimerTrigger2(Watermark) COUGHT ERROR : {0}", ex.ToString());
            outputString = "{\"timestamp\": \"" + DateTime.Now + "\", \"id\" : \"novalue\", \"content\" : \"novalue\"}\n";
        }
        finally
        {
            outputBlob = inputBlob + outputString;
            log.LogInformation($"TimerTrigger2(Watermark) Excecution Completed at : {DateTime.Now}");
        }
    }

    ```
  - INSERT NEW Data or Add new file(dummy.data) in the folder associated with the BLOB trigger and Confirm the result of LISTENER Function log
    ```
    insert into logs(content) values('{"id":"test"}');
    ```

  - Refer CloudAppendBlob
    - https://docs.microsoft.com/ja-jp/azure/azure-functions/functions-bindings-storage-blob-output?tabs=csharp-script#usage    











