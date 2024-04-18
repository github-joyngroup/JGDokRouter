using DocDigitizer.Common.DAL;
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
    /// DokRouter DAL Implementation for MongoDB - Only use after setup of the MainStorageHelper by invoking Startup method on that class
    /// </summary>
    public class DokRouterMongoDAL: IDokRouterDAL
    {
        #region Configuration and respective versioning

        public DokRouterEngineConfiguration GetLatestEngineConfiguration()
        {
            return GenericMongoDAL<DokRouterEngineConfigurationForMongo, DokRouterEngineConfigurationLatestMapper>.SearchManyPaginated(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Page = 1, PageSize = 1
            }).ResultSet.FirstOrDefault()?.DokRouterEngineConfiguration;
        }

        public DokRouterEngineConfiguration GetEngineConfigurationByHash(string hash)
        {
            return GenericMongoDAL<DokRouterEngineConfigurationForMongo, DokRouterEngineConfigurationArchiveMapper>.SearchManyPaginated(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Properties = new List<DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties>()
                {
                    new DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties(DokRouterEngineConfigurationForMongoProperties.Hash, DocDigitizer.Common.DAL.EntityTable.EntitySearchPropertiesOperator.Equal, hash)
                },
                Page = 1,
                PageSize = 1,
            }).ResultSet.FirstOrDefault()?.DokRouterEngineConfiguration;
        }

        public void SaveOrUpdateEngineConfiguration(DokRouterEngineConfiguration engineConfiguration)
        {
            GenericMongoDAL<DokRouterEngineConfigurationForMongo, DokRouterEngineConfigurationArchiveMapper>.UpdateObject(new DokRouterEngineConfigurationForMongo(engineConfiguration));
        }

        #endregion

        #region Pipeline Instances and execution methods

        public (List<PipelineInstance> result, int lastPage) GetRunningInstances(int pageNumber)
        {
            var baseResult = GenericMongoDAL<PipelineInstanceForMongo, PipelineInstanceRunningMapper>.SearchManyPaginated(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Page = pageNumber
            });

            return (baseResult.ResultSet.Select(r => r.PipelineInstance).ToList(), baseResult.LastPage);
        }

        public void SaveOrUpdatePipelineInstance(PipelineInstance pipelineInstance)
        {
            GenericMongoDAL<PipelineInstanceForMongo, PipelineInstanceRunningMapper>.UpdateObject(new PipelineInstanceForMongo(pipelineInstance));
        }

        public void FinishPipelineInstance(PipelineInstance pipelineInstance)
        {
            //TODO: Should be executed within a transaction
            GenericMongoDAL<PipelineInstanceForMongo, PipelineInstanceFinishedMapper>.UpdateObject(new PipelineInstanceForMongo(pipelineInstance)); 
            GenericMongoDAL<PipelineInstanceForMongo, PipelineInstanceRunningMapper>.DeleteObject(pipelineInstance.Key.PipelineInstanceIdentifier.ToString("N"));
        }

        public void ErrorPipelineInstance(PipelineInstance pipelineInstance)
        {
            //TODO: Should be executed within a transaction
            GenericMongoDAL<PipelineInstanceForMongo, PipelineInstanceErroredMapper>.UpdateObject(new PipelineInstanceForMongo(pipelineInstance));
            GenericMongoDAL<PipelineInstanceForMongo, PipelineInstanceRunningMapper>.DeleteObject(pipelineInstance.Key.PipelineInstanceIdentifier.ToString("N"));
        }

        #endregion
    }
}
