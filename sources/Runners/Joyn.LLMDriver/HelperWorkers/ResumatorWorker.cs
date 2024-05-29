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
using System.Net.Mime;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Joyn.LLMDriver.HelperWorkers
{
    public class ResumatorWorker
    {
        private const int ResumatorPreventThrottlingSleep = 1000; //1 call per second to prevent kickout from the resumator API
        private const string ResumeClassificationValue = "Resume";

        //Static variables
        private static HttpClient _httpClient;
        private static ResumatorWorkerConfiguration _configuration;

        public static void Startup(ResumatorWorkerConfiguration configuration)
        {
            _httpClient = new HttpClient();
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

        #region Update Candidates

        /// <summary>
        /// Check against the resumator API if there are new candidates to synchronize
        /// </summary>
        [JGTimelogClientAspect(ModelParameterIndex = 0, ExecutionIdParameterIndex = 1, ExpectedModelType = JGLogClientKnownModelTypes.ActivityModel, Domain = JGTimelogDomainTable._60_UpdateCandidates)]
        public static void UpdateCandidates(ActivityModel model, Guid executionId, LLMCompanyData companyData)
        {
            try
            {
                // Use the ApplicantLatestApplyDate for the API call
                DateTime? lastResumatorDate = companyData.ApplicantLatestApplyDate;

                // Base API URL from configuration
                string baseApiUrl = _configuration.BaseResumatorApiUrl;

                // Complete API endpoint
                string apiUrl = lastResumatorDate.HasValue
                    ? $"{baseApiUrl}/applicants/from_apply_date/{lastResumatorDate.Value.ToString("yyyy-MM-dd")}?apikey={companyData.ResumatorApiKey}"
                    : $"{baseApiUrl}/applicants?apikey={companyData.ResumatorApiKey}";

                DDLogger.LogDebug<ResumatorWorker>($"{executionId} - Will get candidate list from resumator from url: {apiUrl.Substring(0, apiUrl.IndexOf("apikey"))}");

                // Make the API call to fetch the differential list of CVs
                HttpResponseMessage response = _httpClient.GetAsync(apiUrl).Result;
                response.EnsureSuccessStatusCode();

                string responseData = response.Content.ReadAsStringAsync().Result;
                
                DDLogger.LogInfo<ResumatorWorker>($"{executionId} - Got candidate list response from resumator with length: {responseData.Length}");
                // Process the response data as needed
                var candidates = ProcessCandidateList(responseData, companyData);
                DDLogger.LogInfo<ResumatorWorker>($"{executionId} - Parsed candidate list response to: {candidates.Count}");

                //Full list of tuples - CadidateEmail, ApplicationId, this will be used to trigger further processing of these pairs
                List<(string candidateEmail, string applicationId)> candidatesAndApplications = new List<(string candidateEmail, string applicationId)>();
                //Update the database
                if (candidates.Any())
                {
                    foreach (var candidate in candidates)
                    {
                        //Save the candidate and the application for later use
                        candidatesAndApplications.Add((candidate.Email, candidate.Applications[companyData.CompanyIdentifier].First().Id));

                        var dbCandidate = ApplicantForMongoDAL.GetApplicantByEmail(candidate.Email);
                        if (dbCandidate == null)
                        {
                            //First instance of the candidate, just save it
                            ApplicantForMongoDAL.SaveOrUpdateApplicant(candidate);
                        }
                        else
                        {
                            //Applicant already exists, add his applicancies to the list
                            foreach (var application in candidate.Applications.SelectMany(a => a.Value))
                            {
                                if (!dbCandidate.Applications.ContainsKey(application.CompanyIdentifier)) { dbCandidate.Applications.Add(application.CompanyIdentifier, new List<Application>()); }
                                if (!dbCandidate.Applications[application.CompanyIdentifier].Any(a => a.Id == application.Id))
                                {
                                    dbCandidate.Applications[application.CompanyIdentifier].Add(application);
                                }
                            }

                            //Update candidate in the database
                            ApplicantForMongoDAL.SaveOrUpdateApplicant(dbCandidate);
                        }

                        // Update the last iteration date if this job is most recent
                        companyData.ApplicantLatestApplyDate = new DateTime(Math.Max((companyData.ApplicantLatestApplyDate ?? DateTime.MinValue).Ticks, candidate.MaxApplyDate.Ticks));
                    }
                }

                //Trigger the download documentation for each applications
                foreach(var candidateAndApplication in candidatesAndApplications)
                {
                    DomainWorker.StartPipelineByDomain(model.TransactionIdentifier,
                                                       _configuration.ResumatorDownloadDocumentsDomainIdentifier,
                                                       _configuration.ResumatorDownloadDocumentsPipeline,
                                                       companyData.CompanyIdentifier, 
                                                       new Dictionary<string, string>()
                                                       {
                                                           { "candidateEmail", candidateAndApplication.candidateEmail },
                                                           { "applicationId", candidateAndApplication.applicationId }
                                                       }, null);
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

        private static List<Applicant> ProcessCandidateList(string jsonString, LLMCompanyData companyData)
        {
            try
            {
                // Deserialize the JSON response to a list of candidates
                var applicants = new List<Applicant>();

                using (JsonDocument doc = JsonDocument.Parse(jsonString))
                {
                    foreach (JsonElement element in doc.RootElement.EnumerateArray())
                    {
                        var applicantId = element.GetProperty("id").GetString();
                        var applicant = LoadFullApplicant(applicantId, companyData);
                        if (applicant != null)
                        {
                            applicants.Add(applicant);
                        }
#if DEBUG
                        //In Debug we do only one iteration
                        break;
#endif
                        Thread.Sleep(ResumatorPreventThrottlingSleep);
                    }
                }

                return applicants;
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

        private static Applicant LoadFullApplicant(string applicantId, LLMCompanyData companyData)
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

                using (JsonDocument doc = JsonDocument.Parse(responseData))
                {
                    return new Applicant
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

        public static void DownloadDocuments(ActivityModel model, Guid executionId, LLMCompanyData companyData, string applicantEmail, string applicationId)
        {
            var applicant = ApplicantForMongoDAL.GetApplicantByEmail(applicantEmail);
            if (applicant == null)
            {
                DDLogger.LogWarn<ResumatorWorker>($"{executionId} - DownloadDocuments - Applicant not found in the database: {applicantEmail}");
                return;
            }

            if (!applicant.Applications.ContainsKey(companyData.CompanyIdentifier))
            {
                DDLogger.LogWarn<ResumatorWorker>($"{executionId} - DownloadDocuments - Applicant {applicantEmail} does not have applications for company {companyData.CompanyIdentifier}");
                return;
            }

            var application = applicant.Applications[companyData.CompanyIdentifier].FirstOrDefault(a => a.Id == applicationId);
            if (application == null)
            {
                DDLogger.LogWarn<ResumatorWorker>($"{executionId} - DownloadDocuments - Applicant {applicantEmail} does not have application {applicationId} for company {companyData.CompanyIdentifier}");
                return;
            }

            if (application.Documents == null) { application.Documents = new List<ApplicationDocument>(); }

            //TODO - OBTAIN THE DOCUMENTS BINARY
            Task<List<ResumatorDocument>> filesTask = BizapisClient.GetResumatorDocuments(applicantEmail, application.ApplyDate.ToString("yyyy-MM-dd"), companyData.CompanyIdentifier);
            filesTask.Wait();

            foreach (var file in filesTask.Result)
            {
                Guid fileIdentifier = Guid.NewGuid();
                //Save content to storage
                string storageFolder = Path.Combine(_configuration.BaseApplicationDocumentsPath, applicantEmail.Substring(0, 3), applicantEmail, companyData.CompanyIdentifier, applicationId);
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
                    ContentType = file.ContentType
                });
            }

            ApplicantForMongoDAL.SaveOrUpdateApplicant(applicant);

            //Obtain the LLMProcessData object
            var llmProcessData = LLMProcessDataDAL.Get(model.DatabaseIdentifier);

            //TODO: HOW TO HANDLE MULTIPLE DOCUMENTS OR HOW TO PICK RESUME FOR PROCESSING??
            if (application.Documents.Any())
            {
                var documentToProcess = application.Documents.First();

                //Add file to the process data so it can be further used in the pipeline
                llmProcessData.ProcessData[LLMProcessDataConstants.FileInformationKey] = new UploadedFileInformation()
                {
                    EnvelopeUuid = Guid.Parse(documentToProcess.Id),
                    OriginalFileName = documentToProcess.FileName,
                    OriginalContentType = documentToProcess.ContentType,
                    LocalFilePath = documentToProcess.FilePath
                }.ToBsonDocument();

                //Add Resume classification to the process data so it can be used for further processing
                LLMDocumentExtraction llmDocumentExtraction = new LLMDocumentExtraction
                {
                    Classification = ResumeClassificationValue
                };
                llmProcessData.ProcessData[LLMProcessDataConstants.LLMDocumentExtractionKey] = llmDocumentExtraction.ToBsonDocument();

                //Save the updated LLMProcessData object
                LLMProcessDataDAL.SaveOrUpdate(llmProcessData);
            }
        }

        #endregion
    }

    public class ResumatorWorkerConfiguration
    {
        public string BaseResumatorApiUrl { get; set; }
        public Guid ResumatorDownloadDocumentsDomainIdentifier { get; set; }
        public Guid ResumatorDownloadDocumentsPipeline { get; set; }

        public string BaseApplicationDocumentsPath { get; set; }
    }

    
}