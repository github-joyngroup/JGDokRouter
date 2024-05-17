using Joyn.DokRouter.Common.Models;

namespace Joyn.DokRouter.Common.Payloads
{
    /// <summary>
    /// Payload to be used when an activity ends
    /// </summary>
    public class EndActivity
    {
        /// <summary>
        /// The unique identifier of the activity execution to end
        /// </summary>
        public ActivityExecutionKey ActivityExecutionKey { get; set; }

        /// <summary>
        /// Whether or not the activity was successful
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// The error message if the activity was not successful
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Update to the external data that will be passed to the activities when they are started and updated when the activities end.
        /// If null, no changes shall be made in the pipeline instance
        /// </summary>
        public byte[]? MarshalledExternalData { get; set; }

        public Dictionary<string, string> ProcessInstanceData { get; set; }
    }
}
