using ADPClient;
using ADPClient.ADPException;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using ChoETL;
using System.Linq;
using System.Collections;

namespace ADPClientDemo
{


    class Program
    {
        /// <summary>
        /// Demonstrating ADP Client connection library using a product url to get data
        /// after connecting
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {

            try
            {
                var sampleJson = File.ReadAllText(@"C:\Users\mohamad.raad\Downloads\new3.json");
                StringBuilder stringBuilder = new StringBuilder();
                using (var JsonData = ChoJSONReader.LoadText(sampleJson)
                       .WithJSONPath("$.workers")
                      )
                {

                    using (var w = new ChoCSVWriter(stringBuilder)
                             .WithFirstLineHeader()
                             .ThrowAndStopOnMissingField(false)
                             .WithMaxScanRows(5000)
                             )
                    {
                        w.Write(JsonData.Select(m => new
                        {
                            AssociateOID = m.associateOID,
                            GenderCode = m.person.genderCode.codeValue,
                            Preferred_GivenName = m.person.preferredName.givenName,
                            Preferred_LastName = m.person.preferredName.familyName1,
                            Preferred_MiddleName = m.person.preferredName.middleName,
                            FirstName = m.person.legalName.givenName,
                            MiddleName = m.person.legalName.middleName,
                            LastName = m.person.legalName.familyName1,
                            FormattedName = m.person.legalName.formattedName,
                            NickName = m.person.legalName.nickName,
                            OriginalHireDate = m.workerDates.originalHireDate,
                            RehireDate = m.workerDates.rehireDate,
                            TerminationDate = m.workerDates.terminationDate,
                            WorkerStatus = m.workerStatus.statusCode.codeValue,
                            WorkerStatusDesc = m.workAssignments[0].assignmentStatus.statusCode.longName,
                            WorkerStatusReason = m.workAssignments[0].assignmentStatus.Keys.Contains("reasonCode") ?
                            m.workAssignments[0].assignmentStatus.reasonCode.shortName : null,
                            EmployeeType = m.workAssignments[0].ContainsKey("workerTypeCode") ?
                            m.workAssignments[0].workerTypeCode.shortName : null,
                            JobTitleCode = m.workAssignments[0].ContainsKey("jobCode") ?
                            m.workAssignments[0].jobCode.codeValue : null,
                            JobTitleDesc = m.workAssignments[0].jobTitle,

                            JobClassCode = m.workAssignments[0].ContainsKey("occupationalClassifications") ? 
                                            ((IEnumerable)m.workAssignments[0].occupationalClassifications).Cast<dynamic>()
                                                .Where(x => x.nameCode.codeValue == "Job Class")
                                                .Select(y => y.classificationCode.codeValue).FirstOrDefault() : null,
                            JobClassDesc = m.workAssignments[0].ContainsKey("occupationalClassifications") ? 
                                                ((IEnumerable)m.workAssignments[0].occupationalClassifications).Cast<dynamic>()
                                                .Where(x => x.nameCode.codeValue == "Job Class")
                                                .Select(y => y.classificationCode.shortName).FirstOrDefault() : null,

                            AreaCode = m.workAssignments[0].ContainsKey("bargainingUnit") ?
                            m.workAssignments[0].bargainingUnit.bargainingUnitCode.codeValue : null,
                            AreaDescription = m.workAssignments[0].ContainsKey("bargainingUnit") ?
                            m.workAssignments[0].bargainingUnit.bargainingUnitCode.shortName : null,
                            CostCenter = m.workAssignments[0].ContainsKey("homeOrganizationalUnits") ?
                             m.workAssignments[0].homeOrganizationalUnits[0].nameCode.codeValue : null,
                            CostCenterName_Short = m.workAssignments[0].ContainsKey("homeOrganizationalUnits") ?
                            m.workAssignments[0].homeOrganizationalUnits[0].nameCode.shortName : null,
                            CostCenterName_Long = m.workAssignments[0].ContainsKey("homeOrganizationalUnits") ?
                            m.workAssignments[0].homeOrganizationalUnits[0].nameCode.longName : null,
                            HomeDepartment = m.workAssignments[0].ContainsKey("homeOrganizationalUnits") ?
                            (m.workAssignments[0].homeOrganizationalUnits.Count >= 2 ?
                            m.workAssignments[0].homeOrganizationalUnits[1].nameCode.codeValue : null) : null,
                            WorkLocationCode = m.workAssignments[0].ContainsKey("homeOrganizationalUnits") ?
                            (m.workAssignments[0].homeOrganizationalUnits.Count >= 3 ?
                            m.workAssignments[0].homeOrganizationalUnits[2].nameCode.codeValue : null) : null,
                            WorkLocation_Short = m.workAssignments[0].ContainsKey("homeOrganizationalUnits") ?
                            (m.workAssignments[0].homeOrganizationalUnits.Count >= 3 ?
                            m.workAssignments[0].homeOrganizationalUnits[2].nameCode.shortName : null) : null,
                            WorkLocation_Long = m.workAssignments[0].ContainsKey("homeOrganizationalUnits") ?
                            (m.workAssignments[0].homeOrganizationalUnits.Count >= 3 ?
                            m.workAssignments[0].homeOrganizationalUnits[2].nameCode.longName : null) : null,
                            WorkAddressStreet_1 = m.workAssignments[0].ContainsKey("homeWorkLocation") ?
                            (m.workAssignments[0].homeWorkLocation.ContainsKey("address") ?
                            m.workAssignments[0].homeWorkLocation.address.lineOne : null) : null,
                            WorkAddressCity = m.workAssignments[0].ContainsKey("homeWorkLocation") ?
                            (m.workAssignments[0].homeWorkLocation.ContainsKey("address") ?
                            m.workAssignments[0].homeWorkLocation.address.cityName : null) : null,
                            WorkAddressStateCode = m.workAssignments[0].ContainsKey("homeWorkLocation") ?
                            (m.workAssignments[0].homeWorkLocation.ContainsKey("address") ?
                            m.workAssignments[0].homeWorkLocation.address.countrySubdivisionLevel1.codeValue : null) : null,
                            WorkAddressState = m.workAssignments[0].ContainsKey("homeWorkLocation") ?
                            (m.workAssignments[0].homeWorkLocation.ContainsKey("address") ?
                            m.workAssignments[0].homeWorkLocation.address.countrySubdivisionLevel1.shortName : null) : null,
                            WorkAddressCountryCode = m.workAssignments[0].ContainsKey("homeWorkLocation") ?
                            (m.workAssignments[0].homeWorkLocation.ContainsKey("address") ?
                            m.workAssignments[0].homeWorkLocation.address.countryCode : null) : null,
                            WorkAddressPostalCode = m.workAssignments[0].ContainsKey("homeWorkLocation") ?
                            (m.workAssignments[0].homeWorkLocation.ContainsKey("address") ?
                            m.workAssignments[0].homeWorkLocation.address.postalCode : null) : null,
                            WorkAddressStreet_2 = m.workAssignments[0].ContainsKey("homeWorkLocation") ?
                            (m.workAssignments[0].homeWorkLocation.ContainsKey("address") ?
                            m.workAssignments[0].homeWorkLocation.address.lineTwo : null) : null,
                            ReportingManagerID = m.workAssignments[0].ContainsKey("reportsTo") ?
                            m.workAssignments[0].reportsTo[0].workerID.idValue : null,
                            ReportingManagerName = m.workAssignments[0].ContainsKey("reportsTo") ?
                            m.workAssignments[0].reportsTo[0].reportsToWorkerName.formattedName : null,
                            AssociateID = m.workAssignments[0].ContainsKey("payrollFileNumber") ?
                            m.workAssignments[0].payrollFileNumber : null,
                            ManagementPositionIndicator = m.workAssignments[0].ContainsKey("ManagementPositionIndicator") ?
                            m.workAssignments[0].ManagementPositionIndicator : null,
                            JobFunctionCode = m.workAssignments[0].ContainsKey("jobFunctionCode") ?
                            m.workAssignments[0].jobFunctionCode.codeValue : null,
                            JobFunctionDescShort = m.workAssignments[0].ContainsKey("jobFunctionCode") ?
                            m.workAssignments[0].jobFunctionCode.shortName : null,
                            JobFunctionDescLong = m.workAssignments[0].ContainsKey("jobFunctionCode") ?
                            m.workAssignments[0].jobFunctionCode.longName : null,
                            OfficerTitle = ((IEnumerable)m.customFieldGroup.CodeFields).Cast<dynamic>()
                                                .Where(x => x.nameCode.codeValue == "Officer Title")
                                                .Select(y => y.codeValue).FirstOrDefault(),
                            workerID_idValue = m.workerID.idValue,
                            DirectReport = ((IEnumerable)m.customFieldGroup.CodeFields).Cast<dynamic>()
                                                .Where(x => x.nameCode.codeValue == "Direct Manager")
                                                .Select(y => y.codeValue).FirstOrDefault()

                        }));
                    }

                    Console.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }




            //        using (var w = new ChoCSVWriter(stringBuilder)
            //            .WithFirstLineHeader()
            //            .ThrowAndStopOnMissingField(false)
            //            .WithMaxScanRows(5000)
            //            )
            //        {

            //            try
            //            {



            //                w.Write(JsonData.Select(m => new
            //                {
            //                    AssociateOID = m.associateOID,
            //                    GenderCode = m.person.genderCode.codeValue,
            //                    Preferred_GivenName = m.person.preferredName.givenName,
            //                    Preferred_LastName = m.person.preferredName.familyName1,
            //                    Preferred_MiddleName = m.person.preferredName.middleName,
            //                    FirstName = m.person.legalName.givenName,
            //                    MiddleName = m.person.legalName.middleName,
            //                    LastName = m.person.legalName.familyName1,
            //                    FormattedName = m.person.legalName.formattedName,
            //                    NickName = m.person.legalName.nickName,
            //                    OriginalHireDate = m.workerDates.originalHireDate,
            //                    RehireDate = m.workerDates.rehireDate,
            //                    TerminationDate = m.workerDates.terminationDate,
            //                    WorkerStatus = m.workerStatus.statusCode.codeValue,
            //                    WorkerStatusDesc = m.workAssignments[0].assignmentStatus.statusCode.longName,
            //                    WorkerStatusReason = m.workAssignments[0].assignmentStatus.Keys.Contains("reasonCode") ?
            //                            m.workAssignments[0].assignmentStatus.reasonCode.shortName : null,
            //                    EmployeeType = m.workAssignments[0].ContainsKey("workerTypeCode") ?
            //                            m.workAssignments[0].workerTypeCode.shortName : null,
            //                    JobTitleCode = m.workAssignments[0].ContainsKey("jobCode") ?
            //                            m.workAssignments[0].jobCode.codeValue : null,
            //                    JobTitleDesc = m.workAssignments[0].jobTitle,
            //                    JobClassCode = m.workAssignments[0].ContainsKey("occupationalClassifications") ?
            //                            m.workAssignments[0].occupationalClassifications[0].classificationCode.codeValue : null,
            //                    JobClassDesc = m.workAssignments[0].ContainsKey("occupationalClassifications") ?
            //                             m.workAssignments[0].occupationalClassifications[0].classificationCode.shortName : null,
            //                    AreaCode = m.workAssignments[0].ContainsKey("bargainingUnit") ?
            //                            m.workAssignments[0].bargainingUnit.bargainingUnitCode.codeValue : null,
            //                    AreaDescription = m.workAssignments[0].ContainsKey("bargainingUnit") ?
            //                            m.workAssignments[0].bargainingUnit.bargainingUnitCode.shortName : null,
            //                    CostCenter = m.workAssignments[0].ContainsKey("homeOrganizationalUnits") ?
            //                             m.workAssignments[0].homeOrganizationalUnits[0].nameCode.codeValue : null,
            //                    CostCenterName_Short = m.workAssignments[0].ContainsKey("homeOrganizationalUnits") ?
            //                            m.workAssignments[0].homeOrganizationalUnits[0].nameCode.shortName : null,
            //                    CostCenterName_Long = m.workAssignments[0].ContainsKey("homeOrganizationalUnits") ?
            //                            m.workAssignments[0].homeOrganizationalUnits[0].nameCode.longName : null,                       
            //                    HomeDepartment = m.workAssignments[0].ContainsKey("homeOrganizationalUnits") ?
            //                            (m.workAssignments[0].homeOrganizationalUnits.Count >= 2 ?
            //                            m.workAssignments[0].homeOrganizationalUnits[1].nameCode.codeValue : null) : null,
            //                    WorkLocationCode = m.workAssignments[0].ContainsKey("homeOrganizationalUnits") ?
            //                            (m.workAssignments[0].homeOrganizationalUnits.Count >= 3 ?
            //                            m.workAssignments[0].homeOrganizationalUnits[2].nameCode.codeValue : null) : null,
            //                    WorkLocation_Short = m.workAssignments[0].ContainsKey("homeOrganizationalUnits") ?
            //                            (m.workAssignments[0].homeOrganizationalUnits.Count >= 3 ?
            //                            m.workAssignments[0].homeOrganizationalUnits[2].nameCode.shortName : null) : null,
            //                    WorkLocation_Long = m.workAssignments[0].ContainsKey("homeOrganizationalUnits") ?
            //                            (m.workAssignments[0].homeOrganizationalUnits.Count >= 3 ?
            //                            m.workAssignments[0].homeOrganizationalUnits[2].nameCode.longName : null) : null,
            //                    WorkAddressStreet_1 = m.workAssignments[0].ContainsKey("homeWorkLocation") ?
            //                            (m.workAssignments[0].homeWorkLocation.ContainsKey("address") ?
            //                            m.workAssignments[0].homeWorkLocation.address.lineOne : null) : null,
            //                    WorkAddressCity = m.workAssignments[0].ContainsKey("homeWorkLocation") ?
            //                            (m.workAssignments[0].homeWorkLocation.ContainsKey("address") ?
            //                            m.workAssignments[0].homeWorkLocation.address.cityName : null) : null,
            //                    WorkAddressStateCode = m.workAssignments[0].ContainsKey("homeWorkLocation") ?
            //                            (m.workAssignments[0].homeWorkLocation.ContainsKey("address") ?
            //                            m.workAssignments[0].homeWorkLocation.address.countrySubdivisionLevel1.codeValue : null) : null,
            //                    WorkAddressState = m.workAssignments[0].ContainsKey("homeWorkLocation") ?
            //                            (m.workAssignments[0].homeWorkLocation.ContainsKey("address") ?
            //                            m.workAssignments[0].homeWorkLocation.address.countrySubdivisionLevel1.shortName : null) : null,
            //                    WorkAddressCountryCode = m.workAssignments[0].ContainsKey("homeWorkLocation") ?
            //                            (m.workAssignments[0].homeWorkLocation.ContainsKey("address") ?
            //                            m.workAssignments[0].homeWorkLocation.address.countryCode : null) : null,
            //                    WorkAddressPostalCode = m.workAssignments[0].ContainsKey("homeWorkLocation") ?
            //                            (m.workAssignments[0].homeWorkLocation.ContainsKey("address") ?
            //                            m.workAssignments[0].homeWorkLocation.address.postalCode : null) : null,
            //                    WorkAddressStreet_2 = m.workAssignments[0].ContainsKey("homeWorkLocation") ?
            //                            (m.workAssignments[0].homeWorkLocation.ContainsKey("address") ?
            //                            m.workAssignments[0].homeWorkLocation.address.lineTwo : null) : null,
            //                    ReportingManagerID = m.workAssignments[0].ContainsKey("reportsTo") ?
            //                            m.workAssignments[0].reportsTo[0].workerID.idValue : null,
            //                    ReportingManagerName = m.workAssignments[0].ContainsKey("reportsTo") ?
            //                            m.workAssignments[0].reportsTo[0].reportsToWorkerName.formattedName : null,
            //                    AssociateID = m.workAssignments[0].ContainsKey("payrollFileNumber") ?
            //                            m.workAssignments[0].payrollFileNumber : null,
            //                    ManagementPositionIndicator = m.workAssignments[0].ContainsKey("ManagementPositionIndicator") ?
            //                            m.workAssignments[0].ManagementPositionIndicator : null,
            //                    JobFunctionCode = m.workAssignments[0].ContainsKey("jobFunctionCode") ?
            //                            m.workAssignments[0].jobFunctionCode.codeValue : null,
            //                    JobFunctionDescShort = m.workAssignments[0].ContainsKey("jobFunctionCode") ?
            //                            m.workAssignments[0].jobFunctionCode.shortName : null,
            //                    JobFunctionDescLong = m.workAssignments[0].ContainsKey("jobFunctionCode") ?
            //                            m.workAssignments[0].jobFunctionCode.longName : null,
            //                    //OfficerTitle = m.customFieldGroup.ContainsKey("codeFields") ?
            //                    //        m.customFieldGroup.codeFields[0].codeValue : null,
            //                    OfficerTitle = ((IEnumerable) m.customFieldGroup).Cast<dynamic>().Where(x => x.nameCode.codeValue == "Officer Title").Select(x => x.codeValue),
            //                    workerID_idValue = m.workerID.idValue
            //                }));

            //            }
            //            catch (Exception ex)
            //            {
            //                Console.ReadLine();
            //            }

            //            //w.Write(JsonData);
            //        }
            //    }

            //    File.WriteAllText("DataNew.csv", stringBuilder.ToString());


            //    //using (StreamWriter writer = new StreamWriter(@"O:\Group - Data Analysts\Online Projects\ADP Interface\Solution\adp-connection-NET\ADPClientDemo\Content\file1.json"))
            //    //{
            //    //    writer.Write(JsonResult);
            //    //}





            //string configFileName = "production.json";
            //    if (args.Length > 0)
            //    {
            //        configFileName = args[0];
            //    }
            //    StreamReader sr = new StreamReader("..\\..\\Content\\config\\" + configFileName);
            //    string clientconfig = sr.ReadToEnd();

            //    ADPAccessToken token = null;

            //    if (String.IsNullOrEmpty(clientconfig))
            //    {
            //        Console.WriteLine("Settings file or default options not available.");
            //    }
            //    else
            //    {
            //        ClientCredentialConfiguration connectionCfg = JSONUtil.Deserialize<ClientCredentialConfiguration>(clientconfig);
            //        ClientCredentialConnection connection = (ClientCredentialConnection)ADPApiConnectionFactory.createConnection(connectionCfg);

            //        try
            //        {
            //            connection.connect();
            //            if (connection.isConnectedIndicator())
            //            {
            //                token = connection.accessToken;



            //                int skip = 0;
            //                string result;
            //                HttpResponseMessage APIResponse;
            //                JObject str = new JObject();
            //                do
            //                {
            //                    APIResponse = connection.getADPData("/hr/v2/workers?$top=100&$skip=" + skip);
            //                    //APIResponse = connection.getADPData("/staffing/v2/job-applications?$top=100&$skip=" + skip);
            //                    // result = connection.getADPData("/hr/v2/workers?$top=100&$skip=" + skip);
            //                    //APIResponse = connection.getADPData("/hr/v2/worker-demographics?$top=100&$skip=" + skip);
            //                    result = APIResponse.Content.ReadAsStringAsync().Result;

            //                    if (!string.IsNullOrEmpty(result))
            //                    {

            //                        var resultArray = JObject.Parse(result);
            //                        str.Merge(resultArray);
            //                        skip = skip + 100;

            //                    }
            //                } while ((int)APIResponse.StatusCode != 204);

            //                string JsonResult = str.ToString(Newtonsoft.Json.Formatting.None);


            //                StringBuilder stringBuilder1 = new StringBuilder();
            //                using (var JsonData = ChoJSONReader.LoadText(JsonResult)

            //                       .WithJSONPath("$.workers")
            //                       //.WithField("GenderCode", jsonPath: "$.person.genderCode.codeValue")
            //                       //.WithField("Preferred_GivenName", jsonPath: "$.person.preferredName.givenName")
            //                       //.WithField("Preferred_LastName", jsonPath: "$.person.preferredName.familyName1")
            //                       //.WithField("Preferred_MiddleName", jsonPath: "$.person.preferredName.middleName")
            //                       //.WithField("FirstName", jsonPath: "$.person.legalName.givenName")
            //                       //.WithField("MiddleName", jsonPath: "$.person.legalName.middleName")
            //                       //.WithField("LastName", jsonPath: "$.person.legalName.familyName1")
            //                       //.WithField("FormattedName", jsonPath: "$.person.legalName.nickName")
            //                       //.WithField("originalHireDate", jsonPath: "$.workerDates.originalHireDate")
            //                       //.WithField("RehireDate", jsonPath: "$.workerDates.rehireDate")
            //                       //.WithField("TerminationDate", jsonPath: "$.workerDates.terminationDate")
            //                       //.WithField("WorkerStatus", jsonPath: "$.workerStatus.statusCode.codeValue")
            //                       //.WithField("WorkerStatusDesc", jsonPath: "$.workAssignments[0].assignmentStatus.statusCode.longName")
            //                       //.WithField("WorkerID", jsonPath: "$.workerID.idValue")

            //                      )
            //                {



            //                    using (var w = new ChoCSVWriter(stringBuilder1)
            //                        .WithFirstLineHeader()
            //                        .ThrowAndStopOnMissingField(false)
            //                        .WithMaxScanRows(5000)
            //                        )
            //                    {
            //                        w.Write(JsonData.Select(m =>
            //                            new
            //                            {
            //                                WorkerID = m.workerID.idValue,
            //                                associateOID = m.associateOID
            //                            }
            //                            ));

            //                        w.Write(JsonData);
            //                    }
            //                }

            //                File.WriteAllText("DataNew.csv", stringBuilder.ToString());


            //                //using (StreamWriter writer = new StreamWriter(@"O:\Group - Data Analysts\Online Projects\ADP Interface\Solution\adp-connection-NET\ADPClientDemo\Content\file1.json"))
            //                //{
            //                //    writer.Write(JsonResult);
            //                //}
            //            }

            //            //Console.WriteLine("\r\nData: {0} ", str);


            //        }
            //        catch (ADPConnectionException e)
            //        {
            //            Console.WriteLine(e.Message);
            //        }
            //        catch (Exception e)
            //        {
            //            Console.WriteLine(e.Message);
            //        }
            //        Console.Read();
            //    }
            //}
        }
    }
}


//using ADPClient;
//using ADPClient.ADPException;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
//using System;
//using System.IO;
//using System.Linq;
//using ADPClientDemo.Model;
//using System.Net.Http.Headers;
//using System.Collections.Generic;

//namespace ADPClientDemo
//{
//    class Program
//    {
//        /// <summary>
//        /// Demonstrating ADP Client connection library using a product url to get data
//        /// after connecting
//        /// </summary>
//        /// <param name="args"></param>
//        static void Main(string[] args)
//        {
//            //var test = new TestCode();
//            //test.readJSON();

//            string configFileName = "production.json";
//            if (args.Length > 0)
//            {
//                configFileName = args[0];
//            }
//            StreamReader sr = new StreamReader("..\\..\\Content\\config\\" + configFileName);
//            string clientconfig = sr.ReadToEnd();

//            ADPAccessToken token = null;

//            if (String.IsNullOrEmpty(clientconfig))
//            {
//                Console.WriteLine("Settings file or default options not available.");
//            }
//            else
//            {
//                ClientCredentialConfiguration connectionCfg = JSONUtil.Deserialize<ClientCredentialConfiguration>(clientconfig);
//                ClientCredentialConnection connection = (ClientCredentialConnection)ADPApiConnectionFactory.createConnection(connectionCfg);

//                var eventList = new List<Event>();

//                try
//                {
//                    connection.connect();
//                    if (connection.isConnectedIndicator())
//                    {
//                        token = connection.accessToken;



//                        //int responseCode;

//                        //do
//                        //{
//                        //    var eventResponse = connection.getADPData("/core/v1/event-notification-messages");
//                        //    responseCode = (int)eventResponse.StatusCode;
//                        //    var eventResult = eventResponse.Content.ReadAsStringAsync().Result;
//                        //    var ADPMsgID = eventResponse.Headers.GetValues("adp-msg-msgid").FirstOrDefault().ToString();

//                        //    JObject eventJson = JObject.Parse(eventResult);


//                        //    var _event = new Event()
//                        //    {
//                        //        eventID = eventJson["events"][0]["eventID"].ToString(),
//                        //        eventNameCode = eventJson["events"][0]["eventNameCode"]["codeValue"].ToString(),
//                        //        eventStatusCode = eventJson["events"][0]["eventStatusCode"]["codeValue"].ToString(),
//                        //        creationDateTime = eventJson["events"][0]["creationDateTime"].ToString(),
//                        //        recordDateTime = eventJson["events"][0]["recordDateTime"].ToString(),
//                        //        effectiveDateTime = eventJson["events"][0]["effectiveDateTime"].ToString(),
//                        //        originator = eventJson["events"][0]["originator"].SelectToken("associateOID") != null
//                        //            ? eventJson["events"][0]["originator"]["associateOID"].ToString() : null,
//                        //        actor = eventJson["events"][0]["actor"].SelectToken("associateOID") != null
//                        //            ? eventJson["events"][0]["actor"]["associateOID"].ToString() : null,
//                        //        serviceCategoryCode = eventJson["events"][0].SelectToken("serviceCategoryCode") != null
//                        //            ? eventJson["events"][0]["serviceCategoryCode"]["codeValue"].ToString() : null,
//                        //        data = eventJson["events"][0]["data"].ToString().Replace("\r\n", string.Empty)
//                        //    };

//                        //    eventList.Add(_event);

//                        //    var deleteResponse = connection.delADPdata("/core/v1/event-notification-messages" + "/" + ADPMsgID);
//                        //    responseCode = (int)deleteResponse.StatusCode;

//                        //} while (responseCode != 204);

//                        //var eventReport =  eventList.Select(x => string.Join("|", new string[]
//                        //{
//                        //    x.eventID,
//                        //    x.eventNameCode,
//                        //    x.eventStatusCode,
//                        //    x.creationDateTime,
//                        //    x.recordDateTime,
//                        //    x.effectiveDateTime,
//                        //    x.originator,
//                        //    x.actor,
//                        //    x.serviceCategoryCode,
//                        //    x.data
//                        //})).ToList();

//                        //Program.ExportCSV(eventReport, "eventMessages4.8.2022.csv", string.Join("|", new string[] {
//                        //        "eventID", "eventNameCode", "eventStatusCode", "creationDateTime", "recordDateTime", "effectiveDateTime",
//                        //        "originator", "actor", "serviceCategoryCode", "data" }));         

//                        var test = connection.getADPData("/hr/v2/workers");
//                        //int skip = 0;
//                        //string result;
//                        //JObject str = new JObject();
//                        //do
//                        //{
//                        //    // result = connection.getADPData("/hr/v2/workers?$top=100&$skip=" + skip);
//                        //    result = connection.getADPData("/hr/v2/worker-demographics?$top=100&$skip=" + skip).Content.ReadAsStringAsync().Result;

//                        //    if (!string.IsNullOrEmpty(result))
//                        //    {
                               
//                        //        var resultArray = JObject.Parse(result);
//                        //        str.Merge(resultArray);

//                        //    }
                            
//                        //    skip += 100;
//                        //} while (result != "");

//                        //var JsonResult = str.ToString(Newtonsoft.Json.Formatting.None);
//                        //Console.WriteLine("\r\nData: {0} ", str);

//                    }
//                }
//                catch (ADPConnectionException e)
//                {
//                    Console.WriteLine(e.Message);
//                }
//                catch (Exception e)
//                {
//                    Program.ExportCSV(eventList, "eventMessages.csv", string.Join("|", new string[] {
//                                "eventID", "eventNameCode", "eventStatusCode", "creationDateTime", "recordDateTime", "effectiveDateTime",
//                                "originator", "actor", "serviceCategoryCode", "data" }));

//                    Console.WriteLine(e.Message);
//                }
//                Console.Read();
//            }
//        }

//        public static void ExportCSV<T>(List<T> list, string filePath, string header)
//        {
//            using (StreamWriter sw = new StreamWriter(filePath))
//            {
//                sw.WriteLine(header);
//                foreach (var line in list)
//                {
//                    sw.WriteLine(line);
//                }
//            }
//        }

//    }
//}
