using Joyn.DokRouter.Common.Models;

namespace Joyn.DokRouter.Common.Payloads
{
    /// <summary>
    /// Payload for starting an activity, can be used when consumers of the DokRouter want to force some a activity to be started
    /// The activity to be executed is the one flagged as the current index of the pipeline instance
    /// In a normal execution flow, the DokRouter will start activities automatically and so this payload is not needed
    /// </summary>
    public class StartActivityIn
    {
        /// <summary>
        /// The pipeline instance key that the activity belongs to and where we want to start the activity
        /// </summary>
        public PipelineInstanceKey PipelineInstanceKey { get; set; }
    }
}
