using Joyn.DokRouter.Common.Models;

namespace Joyn.DokRouter.Common.DAL
{
    /// <summary>
    /// Interface for the DokRouter DAL, will be used to interact with the persistence layer in whatever flavor it is implemented
    /// </summary>
    public interface IDokRouterDAL
    {
        #region Engine Common Configuration

        public CommonConfigurations GetCommonConfigurations();

        #endregion

        #region Activity Configuration and respective versioning

        List<ActivityConfiguration> GetActivityConfigurations();

        ActivityConfiguration GetArchiveActivityConfigurationByHash(string hash);

        void SaveOrUpdateActivityConfigurationArchive(ActivityConfiguration activityConfiguration);

        #endregion

        #region Pipeline Configuration and respective versioning

        List<PipelineConfiguration> GetPipelineConfigurations();

        PipelineConfiguration GetArchivePipelineConfigurationByHash(string hash);

        void SaveOrUpdatePipelineConfigurationArchive(PipelineConfiguration activityConfiguration);

        #endregion

        #region Pipeline Instances and execution methods

        #endregion
    }
}
