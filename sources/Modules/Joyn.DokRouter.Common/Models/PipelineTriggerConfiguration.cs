using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.Common.Models
{
    public class PipelineTriggerConfiguration
    {
        /// <summary>Identifies this Pipeline Trigger</summary>
        public Guid Identifier{ get; set; }

        /// <summary>Kind of trigger to execute</summary>
        public PipelineTriggerKind Kind { get; set; }

        /// <summary>Kind of instruction to execute - In human Readable text</summary>
        public string KindText { get; set; }

        /// <summary>
        /// Whether or not the pipeline is disabled and should not be used. Disabled pipelines at configuration level will not be available to be used in the engine
        /// </summary>
        public bool Disabled { get; set; }

        /// <summary>
        /// When present, trigger will start the pipeline only if the activity with this identifier returns true on expected precondition field of it's ProcessInstanceData end execution
        /// </summary>
        public Guid? PreConditionActivityIdentifier { get; set; }

        /// <summary>
        /// Field to look for the boolean value within ProcessInstanceData that will state wether or not to start the pipeline
        /// </summary>
        public string ExpectedPreConditionField { get; set; }

        /// <summary>For Kind = TimerFrequency, the pretended frequency for the timer</summary>
        public int? TimeFrequencySeconds { get; set; }
    }

    /// <summary>
    /// The kind of instructions that can be executed by the engine
    /// </summary>
    public enum PipelineTriggerKind
    {
        TimerFrequency = 10,
        TimerAbsolute = 11,
        
        EventKafka = 20,
        
        PoolingDatabase = 30,
    }
}
