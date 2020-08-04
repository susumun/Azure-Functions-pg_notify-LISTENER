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
