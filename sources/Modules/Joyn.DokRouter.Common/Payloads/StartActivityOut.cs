using Joyn.DokRouter.Common.Models;

namespace Joyn.DokRouter.Common.Payloads
{
    /// <summary>
    /// Payload used when the engine requests an activity to be started
    /// </summary>
    public class StartActivityOut
    {
        /// <summary>
        /// The identifer of the activity to be executed
        /// </summary>
        public ActivityExecutionKey ActivityExecutionKey { get; set; }

        /// <summary>
        /// External data serialized, will be passed to the activities when they are started and updated when the activities end.
        /// </summary>
        public byte[] MarshalledExternalData { get; set; }

        /// <summary>
        /// When DokRouter is executed within an web application, this property will contain the URL to be called back to flag the end of the activity
        /// </summary>
        public string CallbackUrl { get; set; }

        /// <summary>
        /// Test mode will allow the engine to execute the entire pipeline without actually executing the activities
        /// When an activity is executed in test mode, it should only return the end of the activity and not actually execute it
        /// </summary>
        public bool TestMode { get; set; }
    }
}
