using DocDigitizer.Common.DAL.SimpleMongo;
using Joyn.DokRouter.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.MongoDAL
{
    public class CommonConfigurationsForMongo : IUniqueIdentifier
    {
        public string Id { get; set; }
        public int DefaultRankOrder { get; set; }

        private CommonConfigurations _commonConfigurations;
        public CommonConfigurations CommonConfigurations
        {
            get
            {
                return _commonConfigurations;
            }
            set
            {
                _commonConfigurations = value;
            }
        }

        public CommonConfigurationsForMongo() { }
        public CommonConfigurationsForMongo(CommonConfigurations commonConfigurations) 
        {
            _commonConfigurations = commonConfigurations;
        }
    }
}
