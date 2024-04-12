using DocDigitizer.Common.DAL.KeyValue;
using Joyn.DokRouter.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.MongoDAL
{
    public class DokRouterEngineConfigurationForMongo : IUniqueIdentifier
    {
        public string Id { get; set; }
        public int DefaultRankOrder { get; set; }

        private DokRouterEngineConfiguration _dokRouterEngineConfiguration;
        public DokRouterEngineConfiguration DokRouterEngineConfiguration
        {
            get
            {
                return _dokRouterEngineConfiguration;
            }
            set
            {
                _dokRouterEngineConfiguration = value;
                Id = value.Hash;
            }
        }

        public DokRouterEngineConfigurationForMongo() { }
        public DokRouterEngineConfigurationForMongo(DokRouterEngineConfiguration dokRouterEngineConfiguration) 
        {
            _dokRouterEngineConfiguration = dokRouterEngineConfiguration;
            Id = dokRouterEngineConfiguration.Hash;
        }
    }
}
