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

    public class CommonConfigurationsMapper : BaseMongoMapper
    {
        public static string CollectionName => "CommonConfigurations";
        public static bool UseTransactions => false;
    }

    public class ActivityConfigurationMapper : BaseMongoMapper
    {
        public static string CollectionName => "ActivityConfigurations";
        public static bool UseTransactions => false;
    }

    public class ActivityConfigurationArchiveMapper : BaseMongoMapper
    {
        public static string CollectionName => "ActivityConfigurationsArchive";
        public static bool UseTransactions => false;
    }

    public class PipelineConfigurationMapper : BaseMongoMapper
    {
        public static string CollectionName => "PipelineConfigurations";
        public static bool UseTransactions => false;
    }

    public class PipelineConfigurationArchiveMapper : BaseMongoMapper
    {
        public static string CollectionName => "PipelineConfigurationsArchive";
        public static bool UseTransactions => false;
    }
}