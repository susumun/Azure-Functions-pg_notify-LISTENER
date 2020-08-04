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