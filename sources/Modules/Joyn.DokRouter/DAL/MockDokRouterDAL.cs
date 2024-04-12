using Joyn.DokRouter.Common.DAL;
using Joyn.DokRouter.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.DAL
{
    /// <summary>
    /// Mock implementation of the DokRouter DAL - it will not persist any data and will always return true
    /// </summary>
    public class MockDokRouterDAL : IDokRouterDAL
    {
        public DokRouterEngineConfiguration GetLatestEngineConfiguration()
        {
            return new DokRouterEngineConfiguration();
        }

        public List<DokRouterEngineConfiguration> GetAllEngineConfiguration()
        {
            return new List<DokRouterEngineConfiguration>();
        }

        public DokRouterEngineConfiguration GetEngineConfigurationByHash(string hash)
        {
            return new DokRouterEngineConfiguration();
        }

        public bool SaveOrUpdateEngineConfiguration(DokRouterEngineConfiguration engineConfiguration)
        {
            return true;
        }

        public List<PipelineInstance> GetAllRunningInstances()
        {
            return new List<PipelineInstance>();
        }

        public bool SaveOrUpdatePipelineInstance(PipelineInstance pipelineInstance)
        {
            return true;
        }

        public bool FinishPipelineInstance(PipelineInstance pipelineInstance)
        {
            return true;
        }
    }
}
