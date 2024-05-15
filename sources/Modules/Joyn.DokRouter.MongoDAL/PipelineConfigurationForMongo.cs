using DocDigitizer.Common.DAL.SimpleMongo;
using Joyn.DokRouter.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.MongoDAL
{
    public class PipelineConfigurationForMongo : IUniqueIdentifier
    {
        public string Id { get; set; }
        public int DefaultRankOrder { get; set; }
        public bool Disabled { get; set; }
        public string Hash { get; set; }

        private PipelineConfiguration _pipelineConfiguration;
        public PipelineConfiguration PipelineConfiguration
        {
            get
            {
                return _pipelineConfiguration;
            }
            set
            {
                _pipelineConfiguration = value;
                Disabled = value.Disabled;
                Hash = value.Hash;
            }
        }

        public PipelineConfigurationForMongo() { }
        public PipelineConfigurationForMongo(PipelineConfiguration pipelineConfiguration) 
        {
            _pipelineConfiguration = pipelineConfiguration;
            Disabled = pipelineConfiguration.Disabled;
            Hash = pipelineConfiguration.Hash;
        }
    }

    public enum PipelineConfigurationForMongoProperties
    {
        Disabled,
        Hash
    }
}
