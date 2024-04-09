using Joyn.DokRouter.Models;
using Joyn.DokRouter.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter
{
    public class DokRouterEngineConfiguration
    {
        public Guid? DefaultPipelineIdentifier { get; set; }

        public string OnStartActivityAssembly { get; set; }
        public string OnStartActivityClass { get; set; }
        public string OnStartActivityMethod { get; set; }

        public List<EDDokRouterEngineConfiguration_Pipelines> Pipelines { get; set; }
    }

    public class EDDokRouterEngineConfiguration_Pipelines
    {
        public string Name { get; set; }
        public Guid Identifier { get; set; }
        public List<EDDokRouterEngineConfiguration_Activities> Activities { get; set; }
    }

    public class EDDokRouterEngineConfiguration_Activities
    {
        public string Name { get; set; }
        public Guid Identifier { get; set; }
        public int OrderNumber { get; set; }
        public bool Disabled { get; set; }

        public ActivityKind Kind { get; set; }
        
        //For Kind == Direct
        public string DirectActivityAssembly { get; set; }
        public string DirectActivityClass { get; set; }
        public string DirectActivityMethod { get; set; }

        //For Kind = HTTP
        public string Url { get; set; }
        
        //For Kind == KafkaEvent
        public string KafkaTopic { get; set; }
    }
}
