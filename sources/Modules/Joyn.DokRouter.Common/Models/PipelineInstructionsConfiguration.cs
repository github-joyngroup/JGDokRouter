using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.Common.Models
{
    public class PipelineInstructionsConfiguration
    {
        /// <summary>Order number of the instruction in the pipeline</summary>
        public int OrderNumber { get; set; }

        /// <summary>Kind of instruction to execute</summary>
        public PipelineInstructionKind Kind { get; set; }

        /// <summary>Kind of instruction to execute - In human Readable text</summary>
        public string KindText { get; set; }

        /// <summary>
        /// Expression that will be evaluated, if it returns false the instruction will be skipped
        /// </summary>
        public string ExecutionCondition { get; set; }

        /// <summary>
        /// For Kind = Activity, the activity identifiers to execute
        /// For Kind = Cycle, the activity identifiers to execute within the cycle
        /// </summary>
        public List<Guid> ActivityIdentifiers { get; set; }

        /// <summary>For Kind = Cycle, the number of cycles to execute.
        /// Can be an direct number or a map to a variable that should exist in the pipeline instance InstanceData object
        /// In the second case, the format is {InstanceData.variable name>}
        /// </summary>
        public string NumberCyclesExpression { get; set; }

        /// <summary>For Kind = Cycle, the expression that will be evaluated to obtain a maximum number of cycles if we want to limit them
        /// Can be an direct number or a map to a variable that should exist in the pipeline instance InstanceData object
        /// In the second case, the format is {InstanceData.variable name>}
        /// </summary>
        public string MaxNumberCyclesExpression { get; set; }

        /// <summary>For Kind = GoTo, the order number of the instruction to jump to</summary>
        public int GoToOrderNumber { get; set; }
    }

    /// <summary>
    /// The kind of instructions that can be executed by the engine
    /// </summary>
    public enum PipelineInstructionKind
    {
        /// <summary>
        /// Executes directly an activity or list of activities
        /// </summary>
        Activity = 10,

        /// <summary>
        /// Repeat a number of times the execution of an activity or list of activities
        /// </summary>
        Cycle = 20,

        /// <summary>
        /// Goes to a specific instruction in the pipeline
        /// </summary>
        GoTo = 30
    }
}
