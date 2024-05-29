using DocDigitizer.Common.DAL;
using DocDigitizer.Common.DAL.SimpleMongo;
using Joyn.DokRouter.Common.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.Serialization;
using System.Security.Policy;

namespace Joyn.LLMDriver.DAL
{
    public class LLMCompanyData : IUniqueIdentifier
    {
        public string Id { get; set; }
        public string CompanyIdentifier { get; set; }
        public int DefaultRankOrder { get; set; }
        public DateTime CreatedAt { get; set; }
     
        public string ResumatorApiKey { get; set; }
        public DateTime? JobsLatestOriginalOpenDate { get; set; }
        public DateTime? ApplicantLatestApplyDate { get; set; }

        public bool CVSynchronizationEnabled { get; set; }
    }

    public enum LLMCompanyDataProperties
    {
        CompanyIdentifier
    }

    public static class LLMCompanyDataDAL
    {
        public static List<LLMCompanyData> ListCompanies()
        {
            //Load Company Data from DB 
            //As, by design, we cannot obtain all data on a single call, we will iterate through the pages until we get all data
            //However, this may cause a problem if we have many Company Datas , as we will be loading all of them in memory
            //Should a limit be imposed? And we would only load up to a limit? If so, we might need to change the way we load the company, maybe iterate on them...
            //However, as we expect to have a limited number of company Data ( < 20), this might suffice for now

            var firstPageResult = GenericMongoDAL<LLMCompanyData, LLMCompanyDataMapper>.SearchManyPaginated(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Page = 1,
            });

            var allPagesTasks = Enumerable.Range(2, firstPageResult.LastPage).Select(pageNumber =>
            {
                return Task.Run(() =>
                {
                    return GenericMongoDAL<LLMCompanyData, LLMCompanyDataMapper>.SearchManyPaginated(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
                    {
                        Page = pageNumber,
                    });
                });
            }).ToArray();

            Task.WaitAll(allPagesTasks);
            List<LLMCompanyData> retCompanyData = new List<LLMCompanyData>();
            retCompanyData.AddRange(firstPageResult.ResultSet);
            retCompanyData.AddRange(allPagesTasks.SelectMany(t => t.Result.ResultSet));

            return retCompanyData;
        }

        public static LLMCompanyData Get(string idOrCompanyIdentifier)
        {
            //Try to get by company identifier, fallb ack to get by id
            var llmCompanyData = GenericMongoDAL<LLMCompanyData, LLMCompanyDataMapper>.SearchOne(new DocDigitizer.Common.DAL.EntityTable.EntitySearch()
            {
                Properties = new List<DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties>()
                {
                    new DocDigitizer.Common.DAL.EntityTable.EntitySearchProperties(LLMCompanyDataProperties.CompanyIdentifier, DocDigitizer.Common.DAL.EntityTable.EntitySearchPropertiesOperator.Equal, idOrCompanyIdentifier)
                }
            });

            if(llmCompanyData == null)
            {
                //fallback to get by id
                llmCompanyData = GenericMongoDAL<LLMCompanyData, LLMCompanyDataMapper>.GetObjectById(idOrCompanyIdentifier);
            }

            return llmCompanyData;
        }
        
        public static void SaveOrUpdate(LLMCompanyData llmCompanyData)
        {
            GenericMongoDAL<LLMCompanyData, LLMCompanyDataMapper>.UpdateObject(llmCompanyData);
        }
    }

    public class LLMCompanyDataMapper : BaseMongoMapper
    {
        public static string CollectionName => "LLMCompanyData";
        public static bool UseTransactions => false;
    }
}
