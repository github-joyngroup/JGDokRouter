using DocDigitizer.Common.DAL;
using DocDigitizer.Common.DAL.SimpleMongo;
using Joyn.DokRouter.Common.Models;
using Joyn.LLMDriver.Models;
using static NHibernate.Engine.Query.CallableParser;

namespace Joyn.LLMDriver.DAL
{
    public class ApplicationForMongo : IUniqueIdentifier
    {
        public string Id { get; set; }
        public int DefaultRankOrder { get; set; }
        public string SourceId { get; set; }

        private ApplicationListItem _application;
        public ApplicationListItem Application
        {
            get
            {
                return _application;
            }
            set
            {
                _application = value;
                SourceId = value.Id;
            }
        }

        public ApplicationForMongo() { }
        public ApplicationForMongo(ApplicationListItem application)
        {
            _application = application;
            SourceId = application.Id;
        }
    }

    public enum ApplicationForMongoProperties
    {
        SourceId
    }

    public class ApplicationForMongoMapper : BaseMongoMapper
    {
        public static string CollectionName => "ApplicationListItem";
        public static bool UseTransactions => false;
    }

    public class ApplicationForMongoDAL
    {
        private static ApplicationForMongo InnerGetApplicationBySourceId(string sourceId)
        {
            return GenericMongoDAL<ApplicationForMongo, ApplicationForMongoMapper>.SearchOne(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Properties = new List<DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties>()
                {
                    new DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties(ApplicationForMongoProperties.SourceId, DocDigitizer.Common.DAL.EntityTable.EntitySearchPropertiesOperator.Equal, sourceId)
                },
            });
        }

        public static ApplicationListItem GetApplicationBySourceId(string sourceId)
        {
            return InnerGetApplicationBySourceId(sourceId)?.Application;
        }


        public static void SaveOrUpdateApplication(ApplicationListItem application)
        {
            //Check if Application already exists
            var dbApplication = InnerGetApplicationBySourceId(application.Id);
            if (dbApplication == null)
            {
                dbApplication = new ApplicationForMongo(application);
            }
            else
            {
                dbApplication.Application = application;
            }
            GenericMongoDAL<ApplicationForMongo, ApplicationForMongoMapper>.UpdateObject(dbApplication);
        }
    }
}
