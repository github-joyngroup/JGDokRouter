
namespace Joyn.DokRouter.Common.Models
{
    /// <summary>
    /// Represents a pipeline instance that is being executed by the engine
    /// </summary>
    public class PipelineInstance
    {
        /// <summary>
        /// The identifier of the instance, will contain both the identifier of the pipeline definition and a unique identifier for this instance
        /// </summary>
        public PipelineInstanceKey Key { get; set; }

        /// <summary>
        /// Pointer to the current activity being executed, will map to an entry of the Activities list of the respective pipeline definition
        /// </summary>
        public int CurrentActivityIndex { get; set; }

        /// <summary>
        /// Timestamp for when the pipeline instance was started
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// External data serialized, will be passed to the activities when they are started and updated when the activities end
        /// </summary>
        public byte[] MarshalledExternalData { get; set; }

        /// <summary>
        /// Current state of the pipeline instance activity executions. The key of the dictionary is the index of the activity in the pipeline definition,
        /// The value is another dictionary with the key being the unique ActivityExecutionKey and the value being the respective ActivityExecution
        /// </summary>
        /// EPocas, changed to Dictionary<int<Dictionary<string, ... as Mongo has problems with complex keys - Should be revisited with some kind of
        /// IBsonSerializer but I was unable to make it work
        public Dictionary<int, Dictionary<ActivityExecutionKey, ActivityExecution>> ActivityExecutions { get; set; }
        //public Dictionary<int, Dictionary<string, ActivityExecution>> ActivityExecutions { get; set; }
    }

    /// <summary>
    /// Represents a single activity whose execution was handled by the engine
    /// </summary>
    public class ActivityExecution
    {
        /// <summary>
        /// The key that identifies the activity execution
        /// </summary>
        public ActivityExecutionKey Key { get; set; }

        /// <summary>
        /// Timestamp for when the activity execution was started
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Timestamp for when the activity execution ended
        /// </summary>
        public DateTime? EndedAt { get; set; }

        /// <summary>
        /// Whether or not the activity execution was successful
        /// </summary>
        public bool? IsSuccess { get; set; }

        /// <summary>
        /// If any error was produced during the execution of the activity, it will be stored here
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
