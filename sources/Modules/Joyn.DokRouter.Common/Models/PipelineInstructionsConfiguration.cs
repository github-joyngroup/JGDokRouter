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

        /// <summary>
        /// Expression that will be evaluated, if it returns false the instruction will be skipped
        /// </summary>
        public string ExecutionCondition { get; set; }

        /// <summary>
        /// For Kind = Activity, the activity identifiers to execute
        /// For Kind = Repeat, the activity identifiers to execute within the cycle
        /// </summary>
        public List<Guid> ActivityIdentifiers { get; set; }

        /// <summary>For Kind = Repeat, the expression that after evaluated will give the number of times to repeat the activity</summary>
        public string RepeatExpression { get; set; }

        /// <summary>For Kind = Repeat, the expression that will be evaluated between each execution that will allow the cycle to be stopped</summary>
        public string RepeatBreakExpression { get; set; }

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
        /// Repeats a number of times the execution of an activity or list of activities
        /// </summary>
        Repeat = 20,

        /// <summary>
        /// Goes to a specific instruction in the pipeline
        /// </summary>
        GoTo = 30
    }
}
