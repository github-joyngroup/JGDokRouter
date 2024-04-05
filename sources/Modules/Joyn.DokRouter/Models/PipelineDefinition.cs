using Joyn.DokRouter.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.Models
{
    public delegate void OnStartActivityHandler(ActivityExecutionKey activityExecutionKey, object externalData);

    internal class PipelineDefinition
    {
        public Guid Identifier { get; set; }
        public string Name { get; set; }
      
        public List<PipelineActivityDefinition> Activities { get; set; }
    }

    internal class PipelineActivityDefinition
    {
        public Guid Identifier { get; set; }
        public string Name { get; set; }
        public int OrderNumber { get; set; }

        public Dictionary<string, object> Configuration { get; set; }
        
        public OnStartActivityHandler OnStartActivity { get; set; }
    }
}
