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

        public DokRouterEngineConfiguration GetEngineConfigurationByHash(string hash)
        {
            return new DokRouterEngineConfiguration();
        }

        public void SaveOrUpdateEngineConfiguration(DokRouterEngineConfiguration engineConfiguration)
        {
        }

        public (List<PipelineInstance> result, int lastPage) GetRunningInstances(int pageNumber)
        {
            return (new List<PipelineInstance>(), 1);
        }

        public void SaveOrUpdatePipelineInstance(PipelineInstance pipelineInstance)
        {
        }

        public void FinishPipelineInstance(PipelineInstance pipelineInstance)
        {
            
        }

        public void ErrorPipelineInstance(PipelineInstance pipelineInstance)
        {
            
        }
    }
}
