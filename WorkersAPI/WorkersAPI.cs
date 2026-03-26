//using ChoETL;
//using System;
//using System.Collections;
//using System.Linq;
//using System.Text;

//namespace WorkersAPI
//{
//    public static class WorkersAPI
//    {
//        public static StringBuilder ParseJson(string json)
//        {
//            Console.WriteLine("Parsing Json response, please wait ...");
//            StringBuilder stringBuilder = new StringBuilder();
//            using (var JsonData = ChoJSONReader.LoadText(json)
//                   .WithJSONPath("$.workers")
//                  )
//            {

//                using (var w = new ChoCSVWriter(stringBuilder)
//                    .WithFirstLineHeader()
//                    .ThrowAndStopOnMissingField(false)
//                    .WithMaxScanRows(5000)
//                    )
//                {
//                    w.Write(JsonData.Select(m => new
//                    {
//                        AssociateOID = m.associateOID,
//                        GenderCode = m.person?.genderCode?.codeValue,
//                        Preferred_GivenName = m.person?.preferredName?.givenName,
//                        Preferred_LastName = m.person?.preferredName?.familyName1,
//                        Preferred_MiddleName = m.person?.preferredName?.middleName,
//                        FirstName = m.person?.legalName?.givenName,
//                        MiddleName = m.person?.legalName?.middleName,
//                        LastName = m.person?.legalName?.familyName1,
//                        FormattedName = m.person?.legalName?.formattedName,
//                        NickName = m.person?.legalName?.nickName,
//                        OriginalHireDate = m.workerDates?.originalHireDate,
//                        RehireDate = m.workerDates?.rehireDate,
//                        TerminationDate = m.workerDates?.terminationDate,
//                        WorkerStatus = m.workerStatus?.statusCode?.codeValue,
//                        WorkerStatusDesc = m.workAssignments?[0]?.assignmentStatus?.statusCode?.longName,
//                        WorkerStatusReason = m.workAssignments?[0]?.assignmentStatus?.Keys.Contains("reasonCode") == true ?
//    m.workAssignments[0].assignmentStatus.reasonCode.shortName : null,
//                        EmployeeType = m.workAssignments?[0]?.ContainsKey("workerTypeCode") == true ?
//    m.workAssignments[0].workerTypeCode.shortName : null,
//                        JobTitleCode = m.workAssignments?[0]?.ContainsKey("jobCode") == true ?
//    m.workAssignments[0].jobCode.codeValue : null,
//                        JobTitleDesc = m.workAssignments?[0]?.jobTitle,
//                        JobClassCode = m.workAssignments?[0]?.ContainsKey("occupationalClassifications") == true ?
//    ((IEnumerable)m.workAssignments[0].occupationalClassifications).Cast<dynamic>()
//        .FirstOrDefault(x => x.nameCode.codeValue == "Job Class")?.classificationCode?.codeValue : null,
//                        JobClassDesc = m.workAssignments?[0]?.ContainsKey("occupationalClassifications") == true ?
//        ((IEnumerable)m.workAssignments[0].occupationalClassifications).Cast<dynamic>()
//        .FirstOrDefault(x => x.nameCode.codeValue == "Job Class")?.classificationCode?.shortName : null,
//                        AreaCode = m.workAssignments?[0]?.ContainsKey("bargainingUnit") == true ?
//    m.workAssignments[0].bargainingUnit.bargainingUnitCode.codeValue : null,
//                        AreaDescription = m.workAssignments?[0]?.ContainsKey("bargainingUnit") == true ?
//    m.workAssignments[0].bargainingUnit.bargainingUnitCode.shortName : null,
//                        CostCenter = m.workAssignments?[0]?.ContainsKey("homeOrganizationalUnits") == true ?
//     m.workAssignments[0].homeOrganizationalUnits[0]?.nameCode?.codeValue : null,
//                        CostCenterName_Short = m.workAssignments?[0]?.ContainsKey("homeOrganizationalUnits") == true ?
//    m.workAssignments[0].homeOrganizationalUnits[0]?.nameCode?.shortName : null,
//                        CostCenterName_Long = m.workAssignments?[0]?.ContainsKey("homeOrganizationalUnits") == true ?
//    m.workAssignments[0].homeOrganizationalUnits[0]?.nameCode?.longName : null,
//                        HomeDepartment = m.workAssignments?[0]?.homeOrganizationalUnits?.Count >= 2 ?
//    m.workAssignments[0].homeOrganizationalUnits[1]?.nameCode?.codeValue : null,
//                        WorkLocationCode = m.workAssignments?[0]?.homeOrganizationalUnits?.Count >= 3 ?
//    m.workAssignments[0].homeOrganizationalUnits[2]?.nameCode?.codeValue : null,
//                        WorkLocation_Short = m.workAssignments?[0]?.homeOrganizationalUnits?.Count >= 3 ?
//    m.workAssignments[0].homeOrganizationalUnits[2]?.nameCode?.shortName : null,
//                        WorkLocation_Long = m.workAssignments?[0]?.homeOrganizationalUnits?.Count >= 3 ?
//    m.workAssignments[0].homeOrganizationalUnits[2]?.nameCode?.longName : null,
//                        WorkAddressStreet_1 = m.workAssignments?[0]?.homeWorkLocation?.ContainsKey("address") == true ?
//    m.workAssignments[0].homeWorkLocation.address?.lineOne : null,
//                        WorkAddressCity = m.workAssignments?[0]?.homeWorkLocation?.ContainsKey("address") == true ?
//    m.workAssignments[0].homeWorkLocation.address?.cityName : null,
//                        WorkAddressStateCode = m.workAssignments?[0]?.homeWorkLocation?.ContainsKey("address") == true ?
//m.workAssignments[0].homeWorkLocation.address?.countrySubdivisionLevel1?.codeValue : null,
//                        WorkAddressState = m.workAssignments?[0]?.homeWorkLocation?.ContainsKey("address") == true ?
//m.workAssignments[0].homeWorkLocation.address?.countrySubdivisionLevel1?.shortName : null,
//                        WorkAddressCountryCode = m.workAssignments?[0]?.homeWorkLocation?.ContainsKey("address") == true ?
//m.workAssignments[0].homeWorkLocation.address?.countryCode : null,
//                        WorkAddressPostalCode = m.workAssignments?[0]?.homeWorkLocation?.ContainsKey("address") == true ?
//m.workAssignments[0].homeWorkLocation.address?.postalCode : null,
//                        WorkAddressStreet_2 = m.workAssignments?[0]?.homeWorkLocation?.ContainsKey("address") == true ?
//m.workAssignments[0].homeWorkLocation.address?.lineTwo : null,
//                        ReportingManagerID = m.workAssignments?[0]?.reportsTo?.Count > 0 ?
//m.workAssignments[0].reportsTo[0]?.workerID?.idValue : null,
//                        ReportingManagerName = m.workAssignments?[0]?.reportsTo?.Count > 0 ?
//m.workAssignments[0].reportsTo[0]?.reportsToWorkerName?.formattedName : null,
//                        AssociateID = m.workAssignments?[0]?.payrollFileNumber,
//                        ManagementPositionIndicator = m.workAssignments?[0]?.ManagementPositionIndicator,
//                        JobFunctionCode = m.workAssignments?[0]?.ContainsKey("jobFunctionCode") == true ?
//m.workAssignments[0].jobFunctionCode?.codeValue : null,
//                        JobFunctionDescShort = m.workAssignments?[0]?.ContainsKey("jobFunctionCode") == true ?
//m.workAssignments[0].jobFunctionCode?.shortName : null,
//                        JobFunctionDescLong = m.workAssignments?[0]?.ContainsKey("jobFunctionCode") == true ?
//m.workAssignments[0].jobFunctionCode?.longName : null,
//                        OfficerTitle = ((IEnumerable)m.customFieldGroup?.CodeFields)?.Cast<dynamic>()
//.FirstOrDefault(x => x?.nameCode?.codeValue == "Officer Title")?.codeValue,
//                        workerID_idValue = m.workerID?.idValue,
//                        DirectReport = ((IEnumerable)m.customFieldGroup?.CodeFields)?.Cast<dynamic>()
//.FirstOrDefault(x => x?.nameCode?.codeValue == "Direct Manager")?.codeValue


//                    }));
//                }
//            }
//            return stringBuilder;
//        }
//    }
//}

using ChoETL;
using System;
using System.Text;

namespace WorkersAPI
{
    public static class WorkersAPI
    {
        public static StringBuilder ParseJson(string json)
        {
            Console.WriteLine("Parsing Json response, please wait ...");
            StringBuilder stringBuilder = new StringBuilder();

            using (var JsonData = ChoJSONReader.LoadText(json)
                .WithJSONPath("$.workers")
                .Configure(c => c.FlattenNode = true)
                .Configure(c => c.NestedColumnSeparator = '_')
                .Configure(c => c.MaxScanRows = 5000)
            )
            {
                using (var w = new ChoCSVWriter(stringBuilder)
                    .WithFirstLineHeader()
                    .ThrowAndStopOnMissingField(false)
                    .WithMaxScanRows(5000)
                )
                {
                    w.Write(JsonData);
                }
            }

            return stringBuilder;
        }
    }
}