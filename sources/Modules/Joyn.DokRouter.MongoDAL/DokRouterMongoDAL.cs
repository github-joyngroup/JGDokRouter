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
        #region Engine Common Configuration

        public CommonConfigurations GetCommonConfigurations()
        {
            return GenericMongoDAL<CommonConfigurationsForMongo, CommonConfigurationsMapper>.SearchManyPaginated(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Page = 1,
                PageSize = 1
            }).ResultSet.FirstOrDefault()?.CommonConfigurations;
        }

        #endregion

        #region Activity Configuration and respective versioning

        public List<ActivityConfiguration> GetActivityConfigurations()
        {
            //Load Activity configurations from DB 
            //As, by design, we cannot obtain all data on a single call, we will iterate through the pages until we get all data
            //However, this may cause a problem if we have many activity configurations, as we will be loading all of them in memory
            //Should a limit be imposed? And we would only load up to a limit? If so, we might need to change the way we load the activity configurations, maybe load the pipelines first and then only load the required activities?
            //However, as we expect to have a limited number of activities ( < 1000), this might suffice for now

            var firstPageResult = GenericMongoDAL<ActivityConfigurationForMongo, ActivityConfigurationMapper>.SearchManyPaginated(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Page = 1,
                Properties = new List<DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties>()
                {
                    new DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties(ActivityConfigurationForMongoProperties.Disabled, DocDigitizer.Common.DAL.EntityTable.EntitySearchPropertiesOperator.Equal, false)
                }
            });

            var allPagesTasks = Enumerable.Range(2, firstPageResult.LastPage).Select(pageNumber =>
            {
                return Task.Run(() =>
                {
                    return GenericMongoDAL<ActivityConfigurationForMongo, ActivityConfigurationMapper>.SearchManyPaginated(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
                    {
                        Page = pageNumber,
                        Properties = new List<DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties>()
                        {
                            new DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties(ActivityConfigurationForMongoProperties.Disabled, DocDigitizer.Common.DAL.EntityTable.EntitySearchPropertiesOperator.Equal, false)
                        }
                    });
                });
            }).ToArray();

            Task.WaitAll(allPagesTasks);
            List<ActivityConfiguration> baseActivityConfigurations = new List<ActivityConfiguration>();
            baseActivityConfigurations.AddRange(firstPageResult.ResultSet.Select(r => r.ActivityConfiguration));
            baseActivityConfigurations.AddRange(allPagesTasks.SelectMany(t => t.Result.ResultSet.Select(r => r.ActivityConfiguration)));

            return baseActivityConfigurations;
        }

        public ActivityConfiguration GetArchiveActivityConfigurationByHash(string hash)
        {
            return GenericMongoDAL<ActivityConfigurationForMongo, ActivityConfigurationArchiveMapper>.SearchManyPaginated(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Properties = new List<DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties>()
                {
                    new DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties(ActivityConfigurationForMongoProperties.Hash, DocDigitizer.Common.DAL.EntityTable.EntitySearchPropertiesOperator.Equal, hash)
                },
                Page = 1,
                PageSize = 1,
            }).ResultSet.FirstOrDefault()?.ActivityConfiguration;
        }

        public void SaveOrUpdateActivityConfigurationArchive(ActivityConfiguration activityConfiguration)
        {
            GenericMongoDAL<ActivityConfigurationForMongo, ActivityConfigurationArchiveMapper>.UpdateObject(new ActivityConfigurationForMongo(activityConfiguration));
        }

        #endregion

        #region Pipeline Configuration and respective versioning

        public List<PipelineConfiguration> GetPipelineConfigurations()
        {
            //Load pipeline configurations from DB 
            //As, by design, we cannot obtain all data on a single call, we will iterate through the pages until we get all data
            //However, this may cause a problem if we have many pipeline configurations, as we will be loading all of them in memory
            //Should a limit be imposed? And we would only load up to a limit? If so, we might need to change the way we load the pipeline configurations, maybe load the pipelines on demand whenever they are needed?
            //However, as we expect to have a limited number of pipeline configurations ( < 100), this might suffice for now

            var firstPageResult = GenericMongoDAL<PipelineConfigurationForMongo, PipelineConfigurationMapper>.SearchManyPaginated(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Page = 1,
                Properties = new List<DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties>()
                {
                    new DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties(PipelineConfigurationForMongoProperties.Disabled, DocDigitizer.Common.DAL.EntityTable.EntitySearchPropertiesOperator.Equal, false)
                }
            });

            var allPagesTasks = Enumerable.Range(2, firstPageResult.LastPage).Select(pageNumber =>
            {
                return Task.Run(() =>
                {
                    return GenericMongoDAL<PipelineConfigurationForMongo, PipelineConfigurationMapper>.SearchManyPaginated(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
                    {
                        Page = pageNumber,
                        Properties = new List<DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties>()
                        {
                            new DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties(PipelineConfigurationForMongoProperties.Disabled, DocDigitizer.Common.DAL.EntityTable.EntitySearchPropertiesOperator.Equal, false)
                        }
                    });
                });
            }).ToArray();

            Task.WaitAll(allPagesTasks);
            List<PipelineConfiguration> basePipelineConfigurations = new List<PipelineConfiguration>();
            basePipelineConfigurations.AddRange(firstPageResult.ResultSet.Select(r => r.PipelineConfiguration));
            basePipelineConfigurations.AddRange(allPagesTasks.SelectMany(t => t.Result.ResultSet.Select(r => r.PipelineConfiguration)));

            return basePipelineConfigurations;
        }

        public PipelineConfiguration GetArchivePipelineConfigurationByHash(string hash)
        {
            return GenericMongoDAL<PipelineConfigurationForMongo, PipelineConfigurationArchiveMapper>.SearchManyPaginated(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Properties = new List<DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties>()
                {
                    new DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties(PipelineConfigurationForMongoProperties.Hash, DocDigitizer.Common.DAL.EntityTable.EntitySearchPropertiesOperator.Equal, hash)
                },
                Page = 1,
                PageSize = 1,
            }).ResultSet.FirstOrDefault()?.PipelineConfiguration;
        }

        public void SaveOrUpdatePipelineConfigurationArchive(PipelineConfiguration pipelineConfiguration)
        {
            GenericMongoDAL<PipelineConfigurationForMongo, PipelineConfigurationArchiveMapper>.UpdateObject(new PipelineConfigurationForMongo(pipelineConfiguration));
        }

        #endregion

        #region Pipeline Instances and execution methods

        public List<PipelineInstance> GetRunningInstances()
        {
            //Load Pipeline Instances from DB 
            //As, by design, we cannot obtain all data on a single call, we will iterate through the pages until we get all data
            //However, this may cause a problem if we have many pipeline instances, as we will be loading all of them in memory
            //Should a limit be imposed? And we would only load up to a limit? If so, we might need to change the way we load the running pipelines, maybe load up to a point and when those are finished, load the next ones?
            //However, as we expect to have a limited number of parallel pipelines ( < 1000), this might suffice for now

            var firstPageResult = GenericMongoDAL<PipelineInstanceForMongo, PipelineInstanceRunningMapper>.SearchManyPaginated(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Page = 1,
            });

            var allPagesTasks = Enumerable.Range(2, firstPageResult.LastPage).Select(pageNumber =>
            {
                return Task.Run(() =>
                {
                    return GenericMongoDAL<PipelineInstanceForMongo, PipelineInstanceRunningMapper>.SearchManyPaginated(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
                    {
                        Page = pageNumber,
                    });
                });
            }).ToArray();

            Task.WaitAll(allPagesTasks);
            List<PipelineInstance> basePipelineInstances = new List<PipelineInstance>();
            basePipelineInstances.AddRange(firstPageResult.ResultSet.Select(r => r.PipelineInstance));
            basePipelineInstances.AddRange(allPagesTasks.SelectMany(t => t.Result.ResultSet.Select(r => r.PipelineInstance)));

            return basePipelineInstances;

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
