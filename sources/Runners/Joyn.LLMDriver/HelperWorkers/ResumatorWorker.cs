using DocDigitizer.Common.DataStructures.Cartesian;
using DocDigitizer.Common.DataStructures.OCR;
using DocDigitizer.Common.Exceptions;
using DocDigitizer.Common.Extensions;
using DocDigitizer.Common.Logging;
using Google.Cloud.Vision.V1;
using Joyn.LLMDriver.DAL;
using Joyn.LLMDriver.Models;
using Joyn.LLMDriver.PSAspects;
using Joyn.Timelog.Common.Models;
using Microsoft.AspNetCore.Components.Forms;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using NReco.PdfRenderer;
using SharpCompress.Common;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Joyn.LLMDriver.HelperWorkers
{
    public class ResumatorWorker
    {
        private const int ResumatorMaxCallsPerMinute = 4; //it's 80, but we want to be safe
        private const int ResumatorMaxPageSize = 100;

        //Static variables
        private static RateLimitedHttpClient _httpClient;
        private static ResumatorWorkerConfiguration _configuration;

        public static void Startup(ResumatorWorkerConfiguration configuration)
        {
            _httpClient = new RateLimitedHttpClient(ResumatorMaxCallsPerMinute);
            _configuration = configuration;
        }

        #region Update Jobs

        /// <summary>
        /// Check against the resumator API if there are new jobs to synchronize
        /// </summary>
        [JGTimelogClientAspect(ModelParameterIndex = 0, ExecutionIdParameterIndex = 1, ExpectedModelType = JGLogClientKnownModelTypes.ActivityModel, Domain = JGTimelogDomainTable._60_UpdateJobs)]
        public static void UpdateJobs(ActivityModel model, Guid executionId, LLMCompanyData companyData)
        {
            try
            {
                // Use the JobsLatestOriginalOpenDate for the API call
                DateTime? lastJobDate = companyData.JobsLatestOriginalOpenDate;

                // Base API URL from configuration
                string baseApiUrl = _configuration.BaseResumatorApiUrl;

                // Complete API endpoint
                string apiUrl = lastJobDate.HasValue
                    ? $"{baseApiUrl}/jobs/from_open_date/{lastJobDate.Value.ToString("yyyy-MM-dd")}?apikey={companyData.ResumatorApiKey}"
                    : $"{baseApiUrl}/jobs?apikey={companyData.ResumatorApiKey}";

                DDLogger.LogDebug<ResumatorWorker>($"{executionId} - Will get job list from resumator from url: {apiUrl.Substring(0, apiUrl.IndexOf("apikey"))}");

                // Make the API call to fetch the differential list of Jobs
                HttpResponseMessage response = _httpClient.GetAsync(apiUrl).Result;
                response.EnsureSuccessStatusCode();

                string responseData = response.Content.ReadAsStringAsync().Result;
                DDLogger.LogInfo<ResumatorWorker>($"{executionId} - Got job list response from resumator with length: {responseData.Length}");
                // Process the response data as needed
                var jobs = ProcessJobsList(responseData, companyData.CompanyIdentifier);
                DDLogger.LogInfo<ResumatorWorker>($"{executionId} - Parsed job list response to: {jobs.Count} jobs");

                //Update the database
                if (jobs.Any())
                {
                    foreach (var job in jobs)
                    {
                        JobForMongoDAL.SaveOrUpdateJob(job);

                        // Update the last iteration date if this job is most recent
                        companyData.JobsLatestOriginalOpenDate = new DateTime(Math.Max((companyData.JobsLatestOriginalOpenDate ?? DateTime.MinValue).Ticks, job.OriginalOpenDate.Ticks));
                    }
                }

                // Save the updated company data
                LLMCompanyDataDAL.SaveOrUpdate(companyData);
            }
            catch (HttpRequestException ex)
            {
                DDLogger.LogException<ResumatorWorker>($"{executionId} - UpdateJobs - Exception during API call: {ex.Message}", ex);
                throw;
            }
            catch (Exception ex)
            {
                DDLogger.LogException<ResumatorWorker>($"{executionId} - UpdateJobs - Exception in method: {ex.Message}", ex);
                throw;
            }
        }

        private static List<Job> ProcessJobsList(string jsonString, string companyIdentifier)
        {
            try
            {
                // Deserialize the JSON response to a list of candidates
                var jobs = new List<Job>();

                using (JsonDocument doc = JsonDocument.Parse(jsonString))
                {
                    foreach (JsonElement element in doc.RootElement.EnumerateArray())
                    {
                        var job = new Job
                        {
                            Id = element.GetProperty("id").GetString(),
                            CompanyIdentifier = companyIdentifier,
                            Title = element.GetProperty("title").GetString(),
                            Country = element.GetProperty("country_id").GetString(),
                            City = element.GetProperty("city").GetString(),
                            State = element.GetProperty("state").GetString(),
                            Zip = element.GetProperty("zip").GetString(),
                            Department = element.GetProperty("department").GetString(),
                            Description = element.GetProperty("description").GetString(),
                            MinimumSalary = element.GetProperty("minimum_salary").GetString(),
                            MaximumSalary = element.GetProperty("maximum_salary").GetString(),
                            Notes = element.GetProperty("notes").GetString(),
                            OriginalOpenDate = element.GetProperty("original_open_date").GetDateTime(),
                            Type = element.GetProperty("type").GetString(),
                            Status = element.GetProperty("status").GetString(),
                            SendToJobBoards = element.GetProperty("send_to_job_boards").GetString()?.ToLower() == "yes",
                            HiringLead = element.GetProperty("hiring_lead").GetString(),
                            BoardCode = element.GetProperty("board_code").GetString(),
                            InternalCode = element.GetProperty("internal_code").GetString(),
                            Questionnaire = element.GetProperty("questionnaire").GetString()
                        };

                        jobs.Add(job);
                    }
                }

                return jobs;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing JSON data: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Update Applications

        /// <summary>
        /// Check against the resumator API if there are new applications to synchronize. Those deemed new trigger the update candidate pipeline
        /// </summary>
        [JGTimelogClientAspect(ModelParameterIndex = 0, ExecutionIdParameterIndex = 1, ExpectedModelType = JGLogClientKnownModelTypes.ActivityModel, Domain = JGTimelogDomainTable._60_UpdateApplications)]
        public static void UpdateApplications(ActivityModel model, Guid executionId, LLMCompanyData companyData)
        {
            try
            {
                // Use the time interval and pages for the API call
                DateTime? fromDate = companyData.ApplicantSearchFromDate;
                DateTime toDate = companyData.ApplicantSearchToDate ?? DateTime.Now;
                int? page = companyData.ApplicantSearchPage;

                // Base API URL from configuration
                string baseApiUrl = _configuration.BaseResumatorApiUrl;

                // Complete API endpoint
                string apiUrl = $"{baseApiUrl}/applicants/";
                if (fromDate.HasValue) { apiUrl += $"from_apply_date/{fromDate.Value.ToString("yyyy-MM-dd")}"; }
                apiUrl += $"to_apply_date/{toDate.ToString("yyyy-MM-dd")}";
                if (page.HasValue) { apiUrl += $"/page/{page}"; }
                apiUrl += $"?apikey={companyData.ResumatorApiKey}";

                DDLogger.LogDebug<ResumatorWorker>($"{executionId} - Will get applications list from resumator from url: {apiUrl.Substring(0, apiUrl.IndexOf("apikey"))}");

                // Make the API call to fetch the differential list of CVs
                HttpResponseMessage response = _httpClient.GetAsync(apiUrl).Result;
                response.EnsureSuccessStatusCode();

                string responseData = response.Content.ReadAsStringAsync().Result;

                DDLogger.LogInfo<ResumatorWorker>($"{executionId} - Got applications list response from resumator with length: {responseData.Length}");
                // Process the response data as needed
                var applications = ProcessApplicationList(responseData, companyData);
                DDLogger.LogInfo<ResumatorWorker>($"{executionId} - Parsed candidapplicationsate list response to: {applications.Count}");

                //Update the database
                if (applications.Any())
                {
                    foreach (var application in applications)
                    {
                        var dbApplication = ApplicationForMongoDAL.GetApplicationBySourceId(application.Id);
                        if (dbApplication == null)
                        {
                            //First instance of the candidate, just save it
                            ApplicationForMongoDAL.SaveOrUpdateApplication(application);

                            //Trigger the download candidate 
                            DomainWorker.StartPipelineByDomain(model.TransactionIdentifier,
                                                       _configuration.ResumatorUpdateCandidateDomainIdentifier,
                                                       _configuration.ResumatorUpdateCandidatePipeline,
                                                       companyData.CompanyIdentifier,
                                                       new Dictionary<string, string>()
                                                       {
                                                           { "applicationId", application.Id }
                                                       }, null);
                        }
                        else
                        {
                            //Applicant already exists - do nothing as we do not update applications
                            "0".ToString();
                        }
                    }
                }

                //Check if we have more pages to process
                if (applications.Count >= ResumatorMaxPageSize)
                {
                    //We have, just increment the page, next cycle will process it
                    companyData.ApplicantSearchToDate = toDate;
                    companyData.ApplicantSearchPage = (page ?? 1) + 1;
                }
                else
                {
                    //No more pages, next cycle shall start interval on the current end date; end date will be cleared
                    companyData.ApplicantSearchFromDate = toDate;
                    companyData.ApplicantSearchToDate = null;
                    companyData.ApplicantSearchPage = null;
                }

                // Save the updated company data
                LLMCompanyDataDAL.SaveOrUpdate(companyData);
            }
            catch (HttpRequestException ex)
            {
                DDLogger.LogException<ResumatorWorker>($"{executionId} - UpdateCandidates - Exception during API call: {ex.Message}", ex);
                throw;
            }
            catch (Exception ex)
            {
                DDLogger.LogException<ResumatorWorker>($"{executionId} - UpdateCandidates - Exception in method: {ex.Message}", ex);
                throw;
            }
        }

        private static List<ApplicationListItem> ProcessApplicationList(string jsonString, LLMCompanyData companyData)
        {
            try
            {
                // Deserialize the JSON response to a list of applications
                var applications = new List<ApplicationListItem>();

                using (JsonDocument doc = JsonDocument.Parse(jsonString))
                {
                    foreach (JsonElement element in doc.RootElement.EnumerateArray())
                    {
                        var application = new ApplicationListItem
                        {
                            Id = element.GetProperty("id").GetString(),
                            CompanyIdentifier = companyData.CompanyIdentifier,
                            FirstName = element.GetProperty("first_name").GetString(),
                            LastName = element.GetProperty("last_name").GetString(),
                            Phone = element.GetProperty("prospect_phone").GetString(),
                            ApplyDate = element.GetProperty("apply_date").GetDateTime(),
                            JobId = element.GetProperty("job_id").GetString(),
                            JobTitle = element.GetProperty("job_title").GetString(),
                        };

                        applications.Add(application);
                    }
                }


#if DEBUG
                //If debug only process debugN applications at a a time
                int debugN = 1;
                applications = applications.Take(debugN).ToList();
#endif
                return applications;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing JSON data: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Update Candidate

        [JGTimelogClientAspect(ModelParameterIndex = 0, ExecutionIdParameterIndex = 1, ExpectedModelType = JGLogClientKnownModelTypes.ActivityModel, Domain = JGTimelogDomainTable._60_UpdateApplications)]
        public static void UpdateCandidate(ActivityModel model, Guid executionId, LLMCompanyData companyData, string applicationId)
        {
            var applicationListItem = ApplicationForMongoDAL.GetApplicationBySourceId(applicationId);
            if (applicationListItem == null)
            {
                DDLogger.LogWarn<ResumatorWorker>($"{executionId} - UpdateCandidate - Application not found in the database: {applicationId}");
                return;
            }

            var candidate = LoadFullCandidate(applicationId, companyData);
            if (candidate == null)
            {
                DDLogger.LogWarn<ResumatorWorker>($"{executionId} - UpdateCandidate - Resumator Applicant search returned null for: {applicationId}");
                return;
            }

            //Obtain the LLMProcessData object and update with candidate information
            var llmProcessData = LLMProcessDataDAL.Get(model.DatabaseIdentifier);
            llmProcessData.ProcessData[LLMProcessDataConstants.CandidateInformationKey] = new CandidateInformation() { ApplicationId = applicationId, CandidateEmail = candidate.Email }.ToBsonDocument();
            LLMProcessDataDAL.SaveOrUpdate(llmProcessData);

            var dbCandidate = CandidateForMongoDAL.GetCandidateByEmail(candidate.Email);
            if (dbCandidate == null)
            {
                //First instance of the candidate, just save it
                CandidateForMongoDAL.SaveOrUpdateCandidate(candidate);
            }
            else
            {
                //Candidate already exists, add his applicancies to the list
                foreach (var application in candidate.Applications.SelectMany(a => a.Value))
                {
                    if (!dbCandidate.Applications.ContainsKey(application.CompanyIdentifier)) { dbCandidate.Applications.Add(application.CompanyIdentifier, new List<Application>()); }
                    if (!dbCandidate.Applications[application.CompanyIdentifier].Any(a => a.Id == application.Id))
                    {
                        dbCandidate.Applications[application.CompanyIdentifier].Add(application);
                    }
                }

                //Update candidate in the database
                CandidateForMongoDAL.SaveOrUpdateCandidate(dbCandidate);
            }
            applicationListItem.CandidateUpdated = true;
        }

        private static Candidate LoadFullCandidate(string applicantId, LLMCompanyData companyData)
        {
            try
            {
                // Base API URL from configuration
                string baseApiUrl = _configuration.BaseResumatorApiUrl;

                // Complete API endpoint
                string apiUrl = $"{baseApiUrl}/applicants/{applicantId}?apikey={companyData.ResumatorApiKey}";

                // Make the API call to fetch the differential list of CVs
                HttpResponseMessage response = _httpClient.GetAsync(apiUrl).Result;
                response.EnsureSuccessStatusCode();

                string responseData = response.Content.ReadAsStringAsync().Result;

                if (String.IsNullOrWhiteSpace(responseData))
                {
                    //Should not happed, but if it does, return null
                    "0".ToString();
                    return null;
                }
                using (JsonDocument doc = JsonDocument.Parse(responseData))
                {
                    return new Candidate
                    {
                        Email = doc.RootElement.GetProperty("email").GetString(),

                        FirstName = doc.RootElement.GetProperty("first_name").GetString(),
                        LastName = doc.RootElement.GetProperty("last_name").GetString(),

                        Address = doc.RootElement.GetProperty("address").GetString(),
                        Location = doc.RootElement.GetProperty("location").GetString(),
                        Phone = doc.RootElement.GetProperty("phone").GetString(),
                        LinkedInUrl = doc.RootElement.GetProperty("linkedin_url").GetString(),

                        Applications = new Dictionary<string, List<Application>>()
                        {
                            { companyData.CompanyIdentifier, new List<Application>()
                            {
                                new Application()
                                {
                                    Id = doc.RootElement.GetProperty("id").GetString(),
                                    CompanyIdentifier = companyData.CompanyIdentifier,
                                    ApplyDate = doc.RootElement.GetProperty("apply_date").GetDateTime(),
                                    JobId = doc.RootElement.TryGetProperty("jobs", out JsonElement jobsElement) &&
                                                jobsElement.TryGetProperty("job_id", out JsonElement jobIdElement) ? jobIdElement.GetString() : null,
                                    Documents = new List<ApplicationDocument>()
                                }
                            }
                            }
                        }
                    };
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing JSON data: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Download Documents

        ///<summary>
        /// Will download the documents for the application and save them to the storage
        /// Download will be handled by the BizapisClient
        /// Will return the number of documents as they will be needed to trigger the LLM Document Processing
        /// TODO: Handle Bizapis problems or errors so we can retry or log the error
        ///</summary>
        [JGTimelogClientAspect(ModelParameterIndex = 0, ExecutionIdParameterIndex = 1, ExpectedModelType = JGLogClientKnownModelTypes.ActivityModel, Domain = JGTimelogDomainTable._60_UpdateDocuments)]
        public static int UpdateDocuments(ActivityModel model, Guid executionId, LLMCompanyData companyData)
        {
            //Obtain the LLMProcessData object and extract the applicationId and candidate email
            var llmProcessData = LLMProcessDataDAL.Get(model.DatabaseIdentifier);
            if (llmProcessData == null || llmProcessData.ProcessData == null || !llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.CandidateInformationKey))
            {
                DDLogger.LogError<ResumatorWorker>($"{executionId} - UpdateDocuments - Candidate Information not found in process data");
                return 0;
            }
            var candidateInformation = BsonSerializer.Deserialize<CandidateInformation>(llmProcessData.ProcessData[LLMProcessDataConstants.CandidateInformationKey]);

            var candidate = CandidateForMongoDAL.GetCandidateByEmail(candidateInformation.CandidateEmail);
            if (candidate == null)
            {
                DDLogger.LogWarn<ResumatorWorker>($"{executionId} - UpdateDocuments - Candidate not found in the database: {candidateInformation.CandidateEmail}");
                return 0;
            }

            if (!candidate.Applications.ContainsKey(companyData.CompanyIdentifier))
            {
                DDLogger.LogWarn<ResumatorWorker>($"{executionId} - UpdateDocuments - Candidate {candidateInformation.CandidateEmail} does not have applications for company {companyData.CompanyIdentifier}");
                return 0;
            }

            var application = candidate.Applications[companyData.CompanyIdentifier].FirstOrDefault(a => a.Id == candidateInformation.ApplicationId);
            if (application == null)
            {
                DDLogger.LogWarn<ResumatorWorker>($"{executionId} - UpdateDocuments - Candidate {candidateInformation.CandidateEmail} does not have application {candidateInformation.ApplicationId} for company {companyData.CompanyIdentifier}");
                return 0;
            }

            if (application.Documents == null) { application.Documents = new List<ApplicationDocument>(); }

            string unifiedSearch = String.Empty;
            if (!String.IsNullOrEmpty(candidate.Email)) { unifiedSearch = $"{unifiedSearch}{(unifiedSearch.Length > 0 ? " " : "")}{candidate.Email}"; }
            //if (!String.IsNullOrEmpty(candidate.FirstName)) { unifiedSearch = $"{unifiedSearch}{(unifiedSearch.Length > 0 ? " " : "")}{candidate.FirstName}"; }
            //if (!String.IsNullOrEmpty(candidate.LastName)) { unifiedSearch = $"{unifiedSearch}{(unifiedSearch.Length > 0 ? " " : "")}{candidate.LastName}"; }
            //if (!String.IsNullOrEmpty(candidate.Phone)) { unifiedSearch = $"{unifiedSearch}{(unifiedSearch.Length > 0 ? " " : "")}{candidate.Phone}"; }
            Task<List<ResumatorDocument>> filesTask = BizapisClient.GetResumatorDocuments(unifiedSearch, application.ApplyDate.ToString("yyyy-MM-dd"), companyData.CompanyIdentifier);
            filesTask.Wait();

            foreach (var file in filesTask.Result)
            {
                Guid fileIdentifier = Guid.NewGuid();
                //Save content to storage
                string storageFolder = Path.Combine(_configuration.BaseApplicationDocumentsPath, candidateInformation.CandidateEmail.Substring(0, 3), candidateInformation.CandidateEmail, companyData.CompanyIdentifier, candidateInformation.ApplicationId);
                Directory.CreateDirectory(storageFolder);
                string storageFilePath = Path.Combine(storageFolder, fileIdentifier.ToString("n") + Path.GetExtension(file.FileName));

                File.WriteAllBytes(storageFilePath, file.Content);

                string invalidChars = new string(Path.GetInvalidPathChars()) + new string(Path.GetInvalidFileNameChars());
                string sanitized = Regex.Replace(storageFilePath, $"[{Regex.Escape(invalidChars)}]", "");

                //Update the application with the document
                application.Documents.Add(new ApplicationDocument()
                {
                    Id = fileIdentifier.ToString("n"), //Should this come from resumator?
                    FilePath = storageFilePath,
                    FileName = file.FileName,
                    ContentType = file.ContentType,

                    LLMProcessed = false
                });
            }

            CandidateForMongoDAL.SaveOrUpdateCandidate(candidate);
            return filesTask.Result.Count;
        }

        #endregion

        #region Process Resume

        ///<summary>
        /// Will place the document to be processed by the LLM Document Processing pipeline on the corresponding model location
        ///</summary>
        [JGTimelogClientAspect(ModelParameterIndex = 0, ExecutionIdParameterIndex = 1, ExpectedModelType = JGLogClientKnownModelTypes.ActivityModel, Domain = JGTimelogDomainTable._60_PrepareDocumentProcessing)]
        public static void PrepareDocumentProcessing(ActivityModel model, Guid executionId, LLMCompanyData companyData, int documentToProcessIndex)
        {
            //Obtain the LLMProcessData object and extract the applicationId and candidate email
            var llmProcessData = LLMProcessDataDAL.Get(model.DatabaseIdentifier);
            if (llmProcessData == null || llmProcessData.ProcessData == null || !llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.CandidateInformationKey))
            {
                DDLogger.LogError<ResumatorWorker>($"{executionId} - PrepareResumeProcessing - Candidate Information not found in process data");
                return;
            }
            var candidateInformation = BsonSerializer.Deserialize<CandidateInformation>(llmProcessData.ProcessData[LLMProcessDataConstants.CandidateInformationKey]);

            var candidate = CandidateForMongoDAL.GetCandidateByEmail(candidateInformation.CandidateEmail);
            if (candidate == null)
            {
                DDLogger.LogWarn<ResumatorWorker>($"{executionId} - PrepareResumeProcessing - Candidate not found in the database: {candidateInformation.CandidateEmail}");
                return;
            }

            if (!candidate.Applications.ContainsKey(companyData.CompanyIdentifier))
            {
                DDLogger.LogWarn<ResumatorWorker>($"{executionId} - PrepareResumeProcessing - Candidate {candidateInformation.CandidateEmail} does not have applications for company {companyData.CompanyIdentifier}");
                return;
            }

            var application = candidate.Applications[companyData.CompanyIdentifier].FirstOrDefault(a => a.Id == candidateInformation.ApplicationId);
            if (application == null)
            {
                DDLogger.LogWarn<ResumatorWorker>($"{executionId} - PrepareResumeProcessing - Candidate {candidateInformation.CandidateEmail} does not have application {candidateInformation.ApplicationId} for company {companyData.CompanyIdentifier}");
                return;
            }

            if (application.Documents == null || !application.Documents.Any() || application.Documents.Count < documentToProcessIndex) 
            {
                DDLogger.LogWarn<ResumatorWorker>($"{executionId} - PrepareResumeProcessing - Candidate {candidateInformation.CandidateEmail} application {candidateInformation.ApplicationId} for company {companyData.CompanyIdentifier} does not contain document with index {documentToProcessIndex} cannot continue");
                return;
            }

            var documentToProcess = application.Documents[documentToProcessIndex];

            //Add file to the process data so it can be further used in the pipeline
            llmProcessData.ProcessData[LLMProcessDataConstants.FileInformationKey] = new UploadedFileInformation()
            {
                EnvelopeUuid = Guid.Parse(documentToProcess.Id),
                OriginalFileName = documentToProcess.FileName,
                OriginalContentType = documentToProcess.ContentType,
                LocalFilePath = documentToProcess.FilePath
            }.ToBsonDocument();

            //Save the updated LLMProcessData object
            LLMProcessDataDAL.SaveOrUpdate(llmProcessData);
        }

        ///<summary>
        /// Will clear the document to be processed by the LLM Document Processing pipeline to keep the process data clean after processing
        ///</summary>
        [JGTimelogClientAspect(ModelParameterIndex = 0, ExecutionIdParameterIndex = 1, ExpectedModelType = JGLogClientKnownModelTypes.ActivityModel, Domain = JGTimelogDomainTable._60_ClearDocumentProcessing)]
        public static void ClearDocumentProcessing(ActivityModel model, Guid executionId, LLMCompanyData companyData)
        {
            //Obtain the LLMProcessData object and extract the applicationId and candidate email
            var llmProcessData = LLMProcessDataDAL.Get(model.DatabaseIdentifier);
            if (llmProcessData == null || llmProcessData.ProcessData == null)
            {
                DDLogger.LogError<ResumatorWorker>($"{executionId} - ClearResumeProcessing - Candidate Information not found in process data");
                return;
            }

            //Clear file information key 
            llmProcessData.ProcessData.Remove(LLMProcessDataConstants.FileInformationKey);

            //Save the updated LLMProcessData object
            LLMProcessDataDAL.SaveOrUpdate(llmProcessData);
        }

        #endregion
    }

    public class ResumatorWorkerConfiguration
    {
        public string BaseResumatorApiUrl { get; set; }

        public Guid ResumatorUpdateCandidateDomainIdentifier { get; set; }
        public Guid ResumatorUpdateCandidatePipeline { get; set; }

        public string BaseApplicationDocumentsPath { get; set; }
    }

    public class CandidateInformation
    {
        public string ApplicationId { get; set; }
        public string CandidateEmail { get; set; }
    }
}