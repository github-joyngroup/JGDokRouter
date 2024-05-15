using DocDigitizer.Common.DAL.SimpleMongo;
using Joyn.DokRouter.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.MongoDAL
{
    public class ActivityConfigurationForMongo : IUniqueIdentifier
    {
        public string Id { get; set; }
        public int DefaultRankOrder { get; set; }
        public bool Disabled { get; set; }
        public string Hash { get; set; }

        private ActivityConfiguration _activityConfiguration;
        public ActivityConfiguration ActivityConfiguration
        {
            get
            {
                return _activityConfiguration;
            }
            set
            {
                _activityConfiguration = value;
                Disabled = value.Disabled;
                Hash = value.Hash;
            }
        }

        public ActivityConfigurationForMongo() { }
        public ActivityConfigurationForMongo(ActivityConfiguration activityConfiguration) 
        {
            _activityConfiguration = activityConfiguration;
            Disabled = activityConfiguration.Disabled;
            Hash = activityConfiguration.Hash;
        }
    }

    public enum ActivityConfigurationForMongoProperties
    {
        Disabled,
        Hash
    }
}
