using Joyn.DokRouter.Common.DAL;
using Joyn.DokRouter.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.MongoDAL
{
    /// <summary>
    /// Mock implementation of the DokRouter DAL - it will not persist any data and will always return true
    /// </summary>
    public class DokRouterMongoDAL: IDokRouterDAL
    {
        #region Configuration and respective versioning

        public DokRouterEngineConfiguration GetLatestEngineConfiguration()
        {
            var a = MainStorageHelper.EngineConfigurationStorage.ListRegistryObjects();
            var b = a.Data;
            var c = b.FirstOrDefault();
            var d = c?.DokRouterEngineConfiguration;
            return MainStorageHelper.EngineConfigurationStorage.ListRegistryObjects().Data.FirstOrDefault()?.DokRouterEngineConfiguration;
        }

        public List<DokRouterEngineConfiguration> GetAllEngineConfiguration()
        {
            return MainStorageHelper.EngineConfigurationsArchiveStorage.ListRegistryObjects().Data.Select(ecfm => ecfm.DokRouterEngineConfiguration).ToList();
        }

        public DokRouterEngineConfiguration GetEngineConfigurationByHash(string hash)
        {
            return MainStorageHelper.EngineConfigurationsArchiveStorage.ReadObject(hash)?.DokRouterEngineConfiguration;
        }

        public bool SaveOrUpdateEngineConfiguration(DokRouterEngineConfiguration engineConfiguration)
        {
            return MainStorageHelper.EngineConfigurationsArchiveStorage.AddOrUpdateObject(new DokRouterEngineConfigurationForMongo(engineConfiguration));
        }

        #endregion

        #region Pipeline Instances and execution methods

        public List<PipelineInstance> GetAllRunningInstances()
        {
            return MainStorageHelper.RunningInstancesStorage.ListRegistryObjects().Data.Select(pifm => pifm.PipelineInstance).ToList();
        }

        public bool SaveOrUpdatePipelineInstance(PipelineInstance pipelineInstance)
        {
            return MainStorageHelper.RunningInstancesStorage.AddOrUpdateObject(new PipelineInstanceForMongo(pipelineInstance));
        }

        public bool FinishPipelineInstance(PipelineInstance pipelineInstance)
        {
            //TODO: Should be executed within a transaction
            MainStorageHelper.FinishedInstancesStorage.AddOrUpdateObject(new PipelineInstanceForMongo(pipelineInstance));
            MainStorageHelper.RunningInstancesStorage.DeleteObject(pipelineInstance.Key.PipelineInstanceIdentifier.ToString("N"));
            
            return true;
        }

        #endregion
    }
}
