using ADPClient;
using ChoETL;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Threading.Tasks;


namespace EventListener
{
    class Program
    {
        static ADPEntities context = new ADPEntities();
        static async Task<int> Main(string[] args)
        {
            //var context = new ADPEntities();
            ClientCredentialConnection connection = (ClientCredentialConnection)ADPApiConnectionFactory.InitConnection();

            var eventList = new List<tb_ADP_Event_Messages>();

            try
            {
                await connection.connectAsync();
                if (connection.isConnectedIndicator())
                {
                    int responseCode;

                    do
                    {
                        var eventResponse = await connection.getADPDataAsync("/core/v1/event-notification-messages");
                        responseCode = (int)eventResponse.StatusCode;

                        if (responseCode == 204)
                            return 0;

                        var eventResult = eventResponse.Content.ReadAsStringAsync().Result;
                        var ADPMsgID = eventResponse.Headers.GetValues("adp-msg-msgid").FirstOrDefault().ToString();

                        JObject eventJson = JObject.Parse(eventResult);
                        string eventNameCode = eventJson["events"][0]["eventNameCode"]["codeValue"].ToString();


                        if (eventNameCode == "job-requisition change" || eventNameCode == "job-requisition status change")
                        {
                            var _deleteResponse = await connection.delADPdataAsync("/core/v1/event-notification-messages" + "/" + ADPMsgID);
                            if ((int)_deleteResponse.StatusCode != 200)
                                throw new Exception("Deleting notification message failed");
                            continue;
                        }

                        var _event = new tb_ADP_Event_Messages()
                        {
                            // Use 'eventID' if available, otherwise use 'applicationID' from 'originator'
                            eventID = eventJson["events"][0]["eventID"] != null
                                ? eventJson["events"][0]["eventID"].ToString()
                                : null,

                            // Check for 'applicationID' and assign appropriately
                            applicationID = eventJson["events"][0].SelectToken("applicationID") != null
                                ? eventJson["events"][0]["applicationID"].ToString()
                                : null,

                            // Existing fields
                            eventNameCode = eventJson["events"][0]["eventNameCode"]["codeValue"].ToString(),
                            creationDateTime = Convert.ToDateTime(eventJson["events"][0]["creationDateTime"]),
                            recordDateTime = Convert.ToDateTime(eventJson["events"][0]["recordDateTime"]),

                            // Check for 'eventStatusCode' and assign appropriately
                            eventStatusCode = eventJson["events"][0]["eventStatusCode"] != null && eventJson["events"][0]["eventStatusCode"]["codeValue"] != null
                                ? eventJson["events"][0]["eventStatusCode"]["codeValue"].ToString()
                                : null,

                            // Handling 'effectiveDateTime' - set to null or a default value if not present
                            effectiveDateTime = eventJson["events"][0]["effectiveDateTime"] != null
                                ? Convert.ToDateTime(eventJson["events"][0]["effectiveDateTime"])
                                : DateTime.Now,
                            originator = eventJson["events"][0]["originator"].SelectToken("associateOID") != null
                                ? eventJson["events"][0]["originator"]["associateOID"].ToString() : null,
                            actor = eventJson["events"][0]["actor"] != null && eventJson["events"][0]["actor"].SelectToken("associateOID") != null
                                ? eventJson["events"][0]["actor"]["associateOID"].ToString() : null,

                            serviceCategoryCode = eventJson["events"][0].SelectToken("serviceCategoryCode") != null
                                ? eventJson["events"][0]["serviceCategoryCode"]["codeValue"].ToString() : null,
                            data = eventJson["events"][0]["data"].ToString().Replace("\r\n", string.Empty),
                            EntryDate = DateTime.Now
                        };

                        var messages = context.tb_ADP_Event_Messages.ToList();

                        if (!((_event.eventNameCode == "job-application-status-change") &&
                               messages.Any(x => x.eventNameCode == _event.eventNameCode && x.data == _event.data)))
                        {
                            context.tb_ADP_Event_Messages.Add(_event);
                            context.SaveChanges();
                        }

                        var deleteResponse = await connection.delADPdataAsync("/core/v1/event-notification-messages" + "/" + ADPMsgID);
                        //if ((int)deleteResponse.StatusCode != 200)
                        //    throw new Exception("Deleting notification message failed");

                    } while (responseCode != 204);

                    return 0;
                }
                else
                    throw new Exception("Connection to ADP API failed");
            }

            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                int errorCode = ex.HResult;
                return (errorCode == 0 ? -1 : errorCode);
            }
        }
    }
}

