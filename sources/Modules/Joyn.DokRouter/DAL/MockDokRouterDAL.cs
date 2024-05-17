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
        public List<ActivityConfiguration> GetActivityConfigurations()
        {
            return new List<ActivityConfiguration>();
        }

        public ActivityConfiguration GetArchiveActivityConfigurationByHash(string hash)
        {
            return new ActivityConfiguration();
        }

        public PipelineConfiguration GetArchivePipelineConfigurationByHash(string hash)
        {
            return new PipelineConfiguration();
        }

        public CommonConfigurations GetCommonConfigurations()
        {
            return new CommonConfigurations();
        }

        public List<PipelineConfiguration> GetPipelineConfigurations()
        {
            return new List<PipelineConfiguration>();
        }

        public void SaveOrUpdateActivityConfigurationArchive(ActivityConfiguration activityConfiguration) { }

        public void SaveOrUpdatePipelineConfigurationArchive(PipelineConfiguration activityConfiguration) { }

        public List<PipelineInstance> GetRunningInstances() { return new List<PipelineInstance>(); }

        public void SaveOrUpdatePipelineInstance(PipelineInstance pipelineInstance) { }

        public void FinishPipelineInstance(PipelineInstance pipelineInstance) { }

        public void ErrorPipelineInstance(PipelineInstance pipelineInstance) { }
    }
}
