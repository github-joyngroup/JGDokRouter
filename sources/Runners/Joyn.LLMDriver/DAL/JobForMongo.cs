using DocDigitizer.Common.DAL;
using DocDigitizer.Common.DAL.SimpleMongo;
using Joyn.DokRouter.Common.Models;
using Joyn.LLMDriver.Models;

namespace Joyn.LLMDriver.DAL
{
    public class JobForMongo : IUniqueIdentifier
    {
        public string Id { get; set; }
        public int DefaultRankOrder { get; set; }
        public string SourceId { get; set; }

        private Job _job;
        public Job Job
        {
            get
            {
                return _job;
            }
            set
            {
                _job = value;
                SourceId = value.Id;
            }
        }

        public JobForMongo() { }
        public JobForMongo(Job job)
        {
            _job = job;
            SourceId = job.Id;
        }
    }

    public enum JobForMongoProperties
    {
        SourceId
    }

    public class JobForMongoMapper : BaseMongoMapper
    {
        public static string CollectionName => "Job";
        public static bool UseTransactions => false;
    }

    public class JobForMongoDAL
    {
        public static Job GetJobById(string jobId)
        {
            return InnerGetJobById(jobId)?.Job;
        }

        private static JobForMongo InnerGetJobById(string jobId)
        {
            return GenericMongoDAL<JobForMongo, JobForMongoMapper>.SearchOne(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Properties = new List<DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties>()
                {
                    new DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties(JobForMongoProperties.SourceId, DocDigitizer.Common.DAL.EntityTable.EntitySearchPropertiesOperator.Equal, jobId)
                },
            });
        }

        public static void SaveOrUpdateJob(Job job)
        {
            //Check if Job already exists
            var dbJob = InnerGetJobById(job.Id);
            if(dbJob == null)
            {
                dbJob = new JobForMongo(job);
            }
            else
            {
                dbJob.Job = job;
            }
            GenericMongoDAL<JobForMongo, JobForMongoMapper>.UpdateObject(dbJob);
        }
    }
}
