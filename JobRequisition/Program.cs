using System.Net.Http;
using System;
using System.Threading.Tasks;
using ADPClient;
using ParametersParser;
using Newtonsoft.Json.Linq;
using System.Data.SqlClient;
using System.Data;
using System.Configuration;

namespace JobRequisition
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
           
            ClientCredentialConnection connection = (ClientCredentialConnection)ADPApiConnectionFactory.InitConnection();
            ParameterParser parser = new ParameterParser("ADP JobRequisition API Utility", "This command line tool pulls Job Requisition metadata from ADP Job Requisition API endpoint");
            parser.Parameter("jobrequisition", ParameterParser.AllowType.Required, "ADP Requisition ID");
        
            try
            {
                parser.Parse(args);
                await connection.connectAsync();
                if (connection.isConnectedIndicator())
                {
                    Console.WriteLine("Getting Job Requisition data");

                    // Adjust the endpoint URL according to ADP Job Requisition API documentation
                    string endpoint = $"/staffing/v1/job-requisitions/{parser["jobrequisition"]}";
                    HttpResponseMessage APIResponse = await connection.getADPDataAsync(endpoint);
                    string result = await APIResponse.Content.ReadAsStringAsync();

                    if (!string.IsNullOrEmpty(result))
                    {
                        JObject jobRequisitionData = JObject.Parse(result);
                        var jobRequisitions = jobRequisitionData["jobRequisitions"];
                        if (jobRequisitions != null)
                        {
                            foreach (var jobRequisition in jobRequisitions)
                            {
                                // Extract required fields
                                string requisitionID = (string)jobRequisition["itemID"]; // Assuming itemID is the RequisitionID
                                string jobTitleCode = (string)jobRequisition.SelectToken("job.jobCode.codeValue");
                                string jobTitleDesc = (string)jobRequisition.SelectToken("job.jobTitle");
                                string costCenterCode = (string)jobRequisition.SelectToken("organizationalUnits[0].nameCode.codeValue");
                                string costCenterName = (string)jobRequisition.SelectToken("organizationalUnits[0].nameCode.shortName");
                                string hiringManagerWorkerID = (string)jobRequisition.SelectToken("hiringManager.workerID.idValue");
                                string occupationalClassificationLongName = (string)jobRequisition.SelectToken("job.occupationalClassifications[0].classificationCode.longName");

                                string Code, Msg = string.Empty;
                                InsertIntoDatabase(requisitionID, jobTitleCode, jobTitleDesc, costCenterCode, costCenterName, hiringManagerWorkerID, 
                                    occupationalClassificationLongName, out Code, out Msg);

                                if (Convert.ToInt32(Code) == 0) return 0;
                                else
                                {
                                    throw new Exception(Msg);
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("No data received for the specified Job Requisition ID.");
                    }
                    return 0;
                }
                else
                {
                    throw new Exception("Connection to ADP API failed");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error occurred: {ex.Message}");
                Console.Error.WriteLine($"Stack Trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }

                Console.Error.WriteLine($"Error Source: {ex.Source}");
                return ex.HResult != 0 ? ex.HResult : -1;
            }

        }

        static void InsertIntoDatabase(string requisitionID, string jobTitleCode, string jobTitleDesc, string costCenterCode, string costCenterName,
            string hiringManagerWorkerID, string occupationalClassificationLongName, out string code, out string message)
        {
            string _connectionString = ConfigurationManager.ConnectionStrings["ADPConnection"].ConnectionString;

            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = new SqlCommand("[dbo].[sp_ADP_WA_Populate_StagingTable]", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.AddWithValue("@RequisitionID", requisitionID);
                    command.Parameters.AddWithValue("@JobTitleCode", jobTitleCode ?? "-1");
                    command.Parameters.AddWithValue("@JobTitleDesc", jobTitleDesc ?? "-1");
                    command.Parameters.AddWithValue("@CostCenterCode", costCenterCode ?? "-1");
                    command.Parameters.AddWithValue("@CostCenterName", costCenterName ?? "-1");
                    command.Parameters.AddWithValue("@HiringManager_WorkerID", hiringManagerWorkerID ?? "-1");
                    command.Parameters.AddWithValue("@Occupational_Classification_LongName", occupationalClassificationLongName ?? "-1");


                    command.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 5) { Direction = ParameterDirection.Output });
                    command.Parameters.Add(new SqlParameter("@Msg", SqlDbType.VarChar, 255) { Direction = ParameterDirection.Output });

                    connection.Open();
                    command.ExecuteNonQuery();

                    code = (string)command.Parameters["@Code"].Value;
                    message = (string)command.Parameters["@Msg"].Value;
                }
            }
        }
    }
}
