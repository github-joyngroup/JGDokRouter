//using DocDigitizer.Common.DAL.SimpleMongo;
//using Joyn.DokRouter.Common.Models;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Joyn.DokRouter.MongoDAL
//{
//    public class DokRouterEngineConfigurationForMongo : IUniqueIdentifier
//    {
//        public string Id { get; set; }
//        public int DefaultRankOrder { get; set; }
//        public string Hash { get; set; }

//        private DokRouterEngineConfiguration _dokRouterEngineConfiguration;
//        public DokRouterEngineConfiguration DokRouterEngineConfiguration
//        {
//            get
//            {
//                return _dokRouterEngineConfiguration;
//            }
//            set
//            {
//                _dokRouterEngineConfiguration = value;
//                Hash = value.Hash;
//            }
//        }

//        public DokRouterEngineConfigurationForMongo() { }
//        public DokRouterEngineConfigurationForMongo(DokRouterEngineConfiguration dokRouterEngineConfiguration) 
//        {
//            _dokRouterEngineConfiguration = dokRouterEngineConfiguration;
//            Hash = dokRouterEngineConfiguration.Hash;
//        }
//    }

//    public enum DokRouterEngineConfigurationForMongoProperties
//    {
//        Hash
//    }
//}
