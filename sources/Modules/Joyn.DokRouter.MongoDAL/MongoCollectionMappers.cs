using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.MongoDAL
{
    public class BaseMongoMapper
    {
        public static string ConnectionString { get; set; }
        public static string DatabaseName { get; set; }

        public static void Startup(string connectionString, string databaseName)
        {
            ConnectionString = connectionString;
            DatabaseName = databaseName;
        }
    }

    public class DokRouterEngineConfigurationLatestMapper : BaseMongoMapper
    {
        public static string CollectionName => "EngineConfiguration";
        public static bool UseTransactions => false;
    }

    public class DokRouterEngineConfigurationArchiveMapper : BaseMongoMapper
    {
        public static string CollectionName => "EngineConfigurationsArchive";
        public static bool UseTransactions => false;
    }

    public class PipelineInstanceRunningMapper : BaseMongoMapper
    {
        public static string CollectionName => "InstancesRunning";
        public static bool UseTransactions => false;
    }

    public class PipelineInstanceFinishedMapper : BaseMongoMapper
    {
        public static string CollectionName => "InstancesFinished";
        public static bool UseTransactions => false;
    }

    public class PipelineInstanceErroredMapper : BaseMongoMapper
    {
        public static string CollectionName => "InstancesErrored";
        public static bool UseTransactions => false;
    }
}