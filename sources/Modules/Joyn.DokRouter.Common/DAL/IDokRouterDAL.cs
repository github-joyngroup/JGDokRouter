using Joyn.DokRouter.Common.Models;

namespace Joyn.DokRouter.Common.DAL
{
    /// <summary>
    /// Interface for the DokRouter DAL, will be used to interact with the persistence layer in whatever flavor it is implemented
    /// </summary>
    public interface IDokRouterDAL
    {
        #region Configuration and respective versioning

        DokRouterEngineConfiguration GetLatestEngineConfiguration();

        List<DokRouterEngineConfiguration> GetAllEngineConfiguration();

        DokRouterEngineConfiguration GetEngineConfigurationByHash(string hash);

        bool SaveOrUpdateEngineConfiguration(DokRouterEngineConfiguration engineConfiguration);

        #endregion

        #region Pipeline Instances and execution methods

        /// <summary>
        /// Will obtain all the pipeline instances that are currently runningf rom the persistence layer
        /// </summary>
        /// <returns></returns>
        List<PipelineInstance> GetAllRunningInstances();

        /// <summary>
        /// Shall create or update the pipeline instance in the persistence layer
        /// </summary>
        bool SaveOrUpdatePipelineInstance(PipelineInstance pipelineInstance);

        /// <summary>
        /// Shall finish the pipeline instance in the persistence layer and do any operations related (clear, archive, etc.)
        /// </summary>
        bool FinishPipelineInstance(PipelineInstance pipelineInstance);

        #endregion
    }
}
