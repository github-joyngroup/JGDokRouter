namespace Joyn.LLMDriver.Models
{
    /// <summary>
    /// This classes and all resume handling should be moved to a specific project that would produce a DLL or nuget package
    /// </summary>

    public class Job
    {
        public string Id { get; set; }
        public string CompanyIdentifier { get; set; }
        public string Title { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string Department { get; set; }
        public string Description { get; set; }
        public string MinimumSalary { get; set; }
        public string MaximumSalary { get; set; }
        public string Notes { get; set; }
        public DateTime OriginalOpenDate { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }

        public bool SendToJobBoards { get; set; }

        public string HiringLead { get; set; }
        public string BoardCode { get; set; }
        public string InternalCode { get; set; }
        public string Questionnaire { get; set; }
    }

    public class ApplicationListItem
    {
        public string Id { get; set; }
        public string CompanyIdentifier { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public DateTime ApplyDate { get; set; }
        public string JobId { get; set; }
        public string JobTitle { get; set; }

        //Control fields
        public bool CandidateUpdated { get; set; }
        public bool ApplicationDocumentsDownloaded { get; set; }
    }

    public class Candidate
    {
        public string Email { get; set; }
        
        public string FirstName { get; set; }
        public string LastName { get; set; }
        
        public string Address { get; set; }
        public string Location { get; set; }
        public string Phone { get; set; }
        public string LinkedInUrl { get; set; }

        /// <summary>
        /// Dictionary of applications by company identifier
        /// </summary>
        public Dictionary<string, List<Application>> Applications { get; set; }
    }

    public class Application
    {
        public string Id { get; set; }
        public string CompanyIdentifier { get; set; }
        public DateTime ApplyDate { get; set; }
        
        public string JobId { get; set; }

        public List<ApplicationDocument> Documents { get; set; }
    }

    public class ApplicationDocument
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string ContentType { get; set; }

        //Control Fields
        public bool LLMProcessed { get; set; }
    }
}
