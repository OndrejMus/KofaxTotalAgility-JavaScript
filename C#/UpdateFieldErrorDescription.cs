using System;
// Common .Net features
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using System.Collections;
// KTA 
using TotalAgility.Sdk;
using Agility.Sdk.Model;
using Agility.Server.Scripting.ScriptAssembly;
// Database
using System.Data;
using System.Data.SqlClient;
 
namespace MyNamespace
{
    
    public class Class1
    {
    public Class1() 
    {
    }
        // Log is global so it can be easily used in whole script without sending it to every function
        public LogCollection log = new LogCollection();

        [StartMethodAttribute()]
        public void RunSetPageExtension(ScriptParameters sp)
        {
            // Log script parameters
            log.AppendScriptParameters(sp);

            try
            {
                // ---- Start logic here ----

                // Call in document postvalidation handling to translate default transformation errors

                // Usual input variables
                string sessionId = sp.InputVariables["SPP_SYSTEM_SESSION_ID"].ToString();   // System session id from server variable
                //string folderId = sp.InputVariables["FOLDER_F938266C4CC640FC8C289D1FE732CD3E"].ToString();  // Expects Folder.InstanceId in Input variables
                string documentId = sp.InputVariables["DOCUMENTID"].ToString(); 

                object[][] fields = sp.InputVariables["Fields"];
                            
                // Quick and dirty solution, created for demo purposes
                Dictionary<string, string> messages = new Dictionary<string, string>();
                //messages.Add("anglicka hlaska","ceska hlaska");
                messages.Add("The field extraction was not certain.","Pole nebylo vytěženo s požadovanou mírou jistoty. Pokud je hodnota chybná, doplňte prosím správnou hodnotu. Pokud je hodnota správná, potvrďte prosím pole.");
                messages.Add("The field cannot be empty. Please provide a value.","Pole nemůže být prázdné. Doplňte prosím správnou hodnotu a potvrďte pole.");
                

                // Get document 
                CaptureDocumentService captureDocumentService = new CaptureDocumentService();
                Agility.Sdk.Model.Capture.Document document = captureDocumentService.GetDocument(sessionId, null, documentId);            
                
                foreach (object[] field in fields)
                {
                    if (field.Length == 0 || field[0].ToString().Length == 0)
                    {
                        continue;
                    }

                    string fieldId = field[0].ToString();
                    
                    // Kontrola jestli existuje požadovaný field
                    if (document.Fields != null && (document.Fields.Exists(x => x.Id == fieldId)))
                    {
                        Agility.Sdk.Model.Capture.RuntimeFieldData fieldData = document.Fields.First(x => x.Id == fieldId);

                        if (fieldData.ErrorDescription == null)
                        {
                            continue;
                        }

                        if (messages.ContainsKey(fieldData.ErrorDescription))
                        {
                            captureDocumentService.UpdateDocumentFieldPropertyValues
                            (
                                sessionId,
                                null,
                                documentId,
                                new Agility.Sdk.Model.Capture.FieldPropertiesCollection()
                                {
                        new Agility.Sdk.Model.Capture.FieldProperties()
                        {
                            Identity = new Agility.Sdk.Model.Capture.RuntimeFieldIdentity()
                            {
                                    Id = fieldId,
                            },
                            PropertyCollection = new Agility.Sdk.Model.Capture.FieldSystemPropertyCollection()
                            {
                                new Agility.Sdk.Model.Capture.FieldSystemProperty()
                                {
                                    SystemFieldIdentity = new Agility.Sdk.Model.Capture.FieldSystemPropertyIdentity()
                                    {
                                        Id = "D0D4F7EB416C4E91BD8A10FC805D5390",
                                        Name = "ErrorDescription"
                                    },
                                    Value = messages[fieldData.ErrorDescription]
                                }
                            }
                        }
                                }
                            );
                        }
                    }
                    
                }
                




                // ---- End logic here ----
                log.AppendLog("Processing concluded");
                //sp.OutputVariables["ProcessingLog"] = log.SerializeLog();   // update log variable name if needed
                sp.OutputVariables["ProcessingLog"] = sp.InputVariables["ProcessingLog"].ToString() + Environment.NewLine + Environment.NewLine + log.SerializeLog();   // append to log
            }
            catch (Exception ex)
            {
                log.WriteToEventLog(ex);
                throw new SystemException(log.SerializeLog(), ex);

                // StackTrace st = new StackTrace(ex, true);
                // var frame = st.GetFrame(st.FrameCount - 1);
                // var lineNumber = frame.GetFileLineNumber();
                // var fileName = frame.GetFileName();
                // var methodName = frame.GetMethod().Name;

                // log.WriteToEventLog();
                // throw new Exception("Error message: "+ex.Message+Environment.NewLine+"Custom log: "+Environment.NewLine+log.SerializeLog() + Environment.NewLine + Environment.NewLine +"Stack trace: "+Environment.NewLine+ ex.StackTrace);

                //throw new Exception("Při zpravoání nastala chyba. Line: " + lineNumber.ToString() + ", Method: " + methodName +
                //    ", FileName: " + fileName + ", error message: " + ex.Message + ", stacktrace:" + ex.StackTrace);
            }


        }


    
        // Classes for logging
        public class LogCollection
        {
            public List<LogRecord> logRecords = new List<LogRecord>();

            // Default constructor used when LogCollection is global
            public LogCollection() {}

            // Constructor which takes ScriptParameters and automaticaly adds them as first record. Used when calling from initialization method (one with [StartMethodAttribute])
            public LogCollection(ScriptParameters sp)
            {
                this.AppendLog("KTA C# script. Script parameters: "+Environment.NewLine + SerializeScriptParameters(sp));
            }

            // Simply append message to log as a new row
            public void AppendLog(string message,
                [System.Runtime.CompilerServices.CallerMemberName] string methodName = "")
            {
                this.logRecords.Add(new LogRecord(message, methodName));
            }

            // Append serialized ScriptParameters in case LogCollection is global and ScriptParameters are not awailable when constructor is called
            public void AppendScriptParameters(ScriptParameters sp,
                [System.Runtime.CompilerServices.CallerMemberName] string methodName = "")
            {
                this.logRecords.Add(new LogRecord(SerializeScriptParameters(sp), methodName));
            }

            // Dump log to event log. If Exception is provided, include it's data
            public void WriteToEventLog(Exception ex = null)
            {
                using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
                {
                    eventLog.Source = "TotalAgility_Script";

                    string formatedMessage = "Log generated by C# script in KTA. Log entries:" + Environment.NewLine + this.SerializeLog();
                    System.Diagnostics.EventLogEntryType eventLogEntryType;

                    if (ex != null)
                    {
                        formatedMessage = formatedMessage + Environment.NewLine + Environment.NewLine + ex.ToString();
                        eventLogEntryType = System.Diagnostics.EventLogEntryType.Error;
                    }
                    else
                    {
                        eventLogEntryType = System.Diagnostics.EventLogEntryType.Information;
                    }
                    eventLog.WriteEntry(formatedMessage, eventLogEntryType);
                }
            }

            // Return string containing names and values of Script parameters passed to C# activity from process
            private static string SerializeScriptParameters(ScriptParameters sp)
            {
                string scriptParams = "Script input variables (first 100 chars):"+Environment.NewLine;
                foreach (DictionaryEntry variable in sp.InputVariables)
                {
                    if (variable.Value != null)
                    {
                        string variableValue = variable.Value.ToString();
                        // Event log is limited to 32k chars so very long variables can exceed this size. Taking first 100 chars should be sufficient for usual variables
                        if (variable.Value.ToString().Length > 100)
                        {
                            variableValue = variable.Value.ToString().Substring(0,100);
                        }
                        scriptParams=scriptParams+"Name: "+variable.Key.ToString()+" type: "+variable.Value.GetType().ToString()+" value: "+variableValue+Environment.NewLine;
                    } 
                    else
                    {
                        scriptParams=scriptParams+"Name: "+variable.Key.ToString();
                    }
                    
                }
                scriptParams = scriptParams+"Script output variables (first 100 chars):"+Environment.NewLine;
                foreach (DictionaryEntry variable in sp.OutputVariables)
                {
                    if (variable.Value != null)
                    {
                        string variableValue = variable.Value.ToString();
                        // Event log is limited to 32k chars so very long variables can exceed this size. Taking first 100 chars should be sufficient for usual variables
                        if (variable.Value.ToString().Length > 100)
                        {
                            variableValue = variable.Value.ToString().Substring(0,100);
                        }
                        scriptParams=scriptParams+"Name: "+variable.Key.ToString()+" type: "+variable.Value.GetType().ToString()+" value: "+variableValue+Environment.NewLine;
                    } 
                    else
                    {
                        scriptParams=scriptParams+"Id: "+variable.Key.ToString()+" value is null"+Environment.NewLine;
                    }
                }
                return scriptParams;                
            }

            public string SerializeLog()
            {
                string result = "";
                foreach (var record in this.logRecords.OrderBy(x => x.DateTime))
                {
                    result = result + record.ToString() + Environment.NewLine;
                }
                return result;
            }

            public class LogRecord
            {
                public DateTime DateTime;
                public string Message;
                public string Method;

                public LogRecord(string message, string method)
                {
                    this.DateTime = DateTime.Now;
                    this.Message = message;
                    this.Method = method;
                    Console.WriteLine(this.ToString());
                }

                public override string ToString()
                {
                    return string.Format("{0} Method: {1} Message: {2}",
                        this.DateTime.ToString("yyyy-MM-dd hh:mm:ss.fff"),
                        this.Method,
                        this.Message);
                }
            }
        }
        // Classes for logging end




    }
}
