//EPocas - 15-05-2024 - Replaced by the new engine based on activity pools and instruction sequencer

//using Joyn.DokRouter.Common.Models;

//namespace Joyn.DokRouter.Common.DAL
//{
//    /// <summary>
//    /// Interface for the DokRouter DAL, will be used to interact with the persistence layer in whatever flavor it is implemented
//    /// </summary>
//    public interface IDokRouterDAL
//    {
//        #region Configuration and respective versioning

//        DokRouterEngineConfiguration GetLatestEngineConfiguration();

//        DokRouterEngineConfiguration GetEngineConfigurationByHash(string hash);

//        void SaveOrUpdateEngineConfiguration(DokRouterEngineConfiguration engineConfiguration);

//        #endregion

//        #region Pipeline Instances and execution methods

//        /// <summary>
//        /// Will obtain, from the persistence layer, the pipeline instances that are currently running
//        /// Notice that this method is paged, so it will return a tuple with the list of instances and the last page number to allow consecutive iterations
//        /// </summary>
//        /// <returns></returns>
//        (List<PipelineInstance> result, int lastPage) GetRunningInstances(int pageNumber);

//        /// <summary>
//        /// Shall create or update the pipeline instance in the persistence layer
//        /// </summary>
//        void SaveOrUpdatePipelineInstance(PipelineInstance pipelineInstance);

//        /// <summary>
//        /// Shall finish the pipeline instance in the persistence layer and do any operations related (clear, archive, etc.)
//        /// </summary>
//        void FinishPipelineInstance(PipelineInstance pipelineInstance);

//        /// <summary>
//        /// Shall mark the pipeline instance as errored in the persistence layer accompanied by the error message
//        /// </summary>
//        void ErrorPipelineInstance(PipelineInstance pipelineInstance);

//        #endregion
//    }
//}
