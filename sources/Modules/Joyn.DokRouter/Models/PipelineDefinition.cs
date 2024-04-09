using Joyn.DokRouter.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.Models
{
    public delegate void OnStartActivityHandler(ActivityExecutionDefinition activityExecutionDefinition, StartActivityOut startActivityOutPayload);
    public delegate void OnExecuteActivityHandler(StartActivityOut startActivityOutPayload);

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
        
        public ActivityExecutionDefinition ExecutionDefinition { get; set; }
    }

    public class ActivityExecutionDefinition
    {
        public ActivityKind Kind { get; set; }

        //For Direct
        public OnExecuteActivityHandler DirectActivityHandler { get; set; }

        //For HTTP
        public string Url { get; set; }
        
        //For Kafka Event
        public string KafkaTopic { get; set; }
    }

    public enum ActivityKind
    {
        Direct = 10,        
        HTTP = 20,
        KafkaEvent = 30
    }
}
