using DocDigitizer.Common.DAL.SimpleMongo;
using Joyn.DokRouter.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.MongoDAL
{
    public class PipelineInstanceForMongo : IUniqueIdentifier
    {
        public string Id { get; set; }
        public int DefaultRankOrder { get; set; }

        private PipelineInstance _pipelineInstance;
        public PipelineInstance PipelineInstance
        {
            get
            {
                return _pipelineInstance;
            }
            set
            {
                _pipelineInstance = value;
                Id = value.Key.PipelineInstanceIdentifier.ToString("N");
            }
        }

        public PipelineInstanceForMongo() { }
        public PipelineInstanceForMongo(PipelineInstance pipelineInstance)
        {
            _pipelineInstance = pipelineInstance;
            Id = pipelineInstance.Key.PipelineInstanceIdentifier.ToString("N");
        }
    }
}
