using DocDigitizer.Common.DAL;
using DocDigitizer.Common.DAL.SimpleMongo;
using Joyn.DokRouter.Common.Models;
using Joyn.LLMDriver.Models;

namespace Joyn.LLMDriver.DAL
{
    public class ApplicantForMongo : IUniqueIdentifier
    {
        public string Id { get; set; }
        public int DefaultRankOrder { get; set; }
        public string SourceId { get; set; }
        public string Email { get; set; }

        private Applicant _applicant;
        public Applicant Applicant
        {
            get
            {
                return _applicant;
            }
            set
            {
                _applicant = value;
                Email = value.Email;
            }
        }

        public ApplicantForMongo() { }
        public ApplicantForMongo(Applicant applicant)
        {
            _applicant = applicant;
            Email = applicant.Email;
        }
    }

    public enum ApplicantForMongoProperties
    {
        Email
    }

    public class ApplicantForMongoMapper : BaseMongoMapper
    {
        public static string CollectionName => "Applicant";
        public static bool UseTransactions => false;
    }

    public class ApplicantForMongoDAL
    {
        private static ApplicantForMongo InnerGetApplicantByEmail(string email)
        {
            return GenericMongoDAL<ApplicantForMongo, ApplicantForMongoMapper>.SearchOne(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Properties = new List<DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties>()
                {
                    new DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties(ApplicantForMongoProperties.Email, DocDigitizer.Common.DAL.EntityTable.EntitySearchPropertiesOperator.Equal, email)
                },
            });
        }

        public static Applicant GetApplicantByEmail(string email)
        {
            return GenericMongoDAL<ApplicantForMongo, ApplicantForMongoMapper>.SearchOne(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Properties = new List<DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties>()
                {
                    new DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties(ApplicantForMongoProperties.Email, DocDigitizer.Common.DAL.EntityTable.EntitySearchPropertiesOperator.Equal, email)
                },
            })?.Applicant;
        }


        public static void SaveOrUpdateApplicant(Applicant applicant)
        {
            //Check if Applicant already exists
            var dbApplicant = InnerGetApplicantByEmail(applicant.Email);
            if (dbApplicant == null)
            {
                dbApplicant = new ApplicantForMongo(applicant);
            }
            else
            {
                dbApplicant.Applicant = applicant;
            }
            GenericMongoDAL<ApplicantForMongo, ApplicantForMongoMapper>.UpdateObject(dbApplicant);
        }
    }
}
