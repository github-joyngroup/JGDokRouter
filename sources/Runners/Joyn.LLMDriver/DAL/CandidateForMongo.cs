using DocDigitizer.Common.DAL;
using DocDigitizer.Common.DAL.SimpleMongo;
using Joyn.DokRouter.Common.Models;
using Joyn.LLMDriver.Models;

namespace Joyn.LLMDriver.DAL
{
    public class CandidateForMongo : IUniqueIdentifier
    {
        public string Id { get; set; }
        public int DefaultRankOrder { get; set; }
        public string SourceId { get; set; }
        public string Email { get; set; }

        private Candidate _candidate;
        public Candidate Candidate
        {
            get
            {
                return _candidate;
            }
            set
            {
                _candidate = value;
                Email = value.Email;
            }
        }

        public CandidateForMongo() { }
        public CandidateForMongo(Candidate candidate)
        {
            _candidate = candidate;
            Email = candidate.Email;
        }
    }

    public enum CandidateForMongoProperties
    {
        Email
    }

    public class CandidateForMongoMapper : BaseMongoMapper
    {
        public static string CollectionName => "Candidate";
        public static bool UseTransactions => false;
    }

    public class CandidateForMongoDAL
    {
        private static CandidateForMongo InnerGetCandidateByEmail(string email)
        {
            return GenericMongoDAL<CandidateForMongo, CandidateForMongoMapper>.SearchOne(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Properties = new List<DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties>()
                {
                    new DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties(CandidateForMongoProperties.Email, DocDigitizer.Common.DAL.EntityTable.EntitySearchPropertiesOperator.Equal, email)
                },
            });
        }

        public static Candidate GetCandidateByEmail(string email)
        {
            return InnerGetCandidateByEmail(email)?.Candidate;
        }


        public static void SaveOrUpdateCandidate(Candidate candidate)
        {
            //Check if Candidate already exists
            var dbCandidate = InnerGetCandidateByEmail(candidate.Email);
            if (dbCandidate == null)
            {
                dbCandidate = new CandidateForMongo(candidate);
            }
            else
            {
                dbCandidate.Candidate = candidate;
            }
            GenericMongoDAL<CandidateForMongo, CandidateForMongoMapper>.UpdateObject(dbCandidate);
        }
    }
}
