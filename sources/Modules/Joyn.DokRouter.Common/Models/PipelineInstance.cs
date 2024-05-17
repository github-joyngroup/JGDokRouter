using System.Text.Json;

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
        /// The identifier of the transaction, if this pipeline is part of a transaction with other pipelines, procedures or processes, they will all share the same Transaction Id
        /// </summary>
        public Guid TransactionIdentifier { get; set; }

        /// <summary>
        /// Name of the pipeline, used mainly for logging and debugging purposes
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Timestamp for when the pipeline instance was started
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Moment when the pipeline instance should be considered as expired
        /// </summary>
        public DateTime PipelineSLAMoment { get; set; }

        /// <summary>
        /// Amount of time that the pipeline instance expired by, this value is calculated by the monitor and doesn't need to be persited
        /// </summary>
        public TimeSpan? PipelineSLAExpiredBy { get; set; }

        /// <summary>
        /// Timestamp for when the pipeline instance was finished
        /// </summary>
        public DateTime? FinishedAt { get; set; }

        /// <summary>
        /// Timestamp for when the pipeline instance was errored
        /// </summary>
        public DateTime? ErroredAt { get; set; }

        /// <summary>
        /// Error message that was produced during the execution of the pipeline instance
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// External data serialized, will be passed to the activities when they are started and updated when the activities end
        /// </summary>
        public byte[] MarshalledExternalData { get; set; }

        /// <summary>
        /// Indicates the step of the pipeline that is currently being executed, will point to the list of instructions of the pipeline definition
        /// (PipelineDefinition.Configuration.InstructionsConfiguration)
        /// </summary>
        public int InstructionPointer { get; set; }

        /// <summary>
        /// Current state of the pipeline instance instruction executions. 
        ///     The key of the dictionary is the index of the instruction in the pipeline definition,
        ///     The value is an InstructionInstance, class that will hold and keep track of the instruction execution
        /// </summary>
        public Dictionary<int, InstructionInstance> InstructionInstances { get; set; }

        /// <summary>
        /// Memory zone for the pipeline instance, it will be used to store data that needs to be shared between activities
        /// </summary>
        public Dictionary<string, string> InstanceData { get; set; }
    }

    public class InstructionInstance
    {
        /// <summary>
        /// Keeps track of the activity instances that are being executed by this instruction
        /// </summary>
        public Dictionary<Guid, ActivityInstance> ActivityInstances { get; set; }
    }

    /// <summary>
    /// Represents a single activity instance that is being executed by the engine
    /// </summary>
    public class ActivityInstance
    {
        /// <summary>
        /// The configuration hash used to create this activity instance.
        /// </summary>
        public string ActivityConfigurationHash { get; set; }

        /// <summary>
        /// Name of the activity, used mainly for logging and debugging purposes
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Moment when the activity was started
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Calculated moment for when this activity should be considered as expired
        /// </summary>
        public DateTime ActivitySLAMoment { get; set; }

        /// <summary>
        /// Amount of time that the activity instance expired by, this value is calculated by the monitor and doesn't need to be persited
        /// </summary>
        public TimeSpan? ActivitySLAExpiredBy { get; set; }

        /// <summary>
        /// Timestamp for when the activity ended
        /// </summary>
        public DateTime? EndedAt { get; set; }

        /// <summary>
        /// Whether or not the activity was successful
        /// </summary>
        public bool? IsSuccess { get; set; }

        /// <summary>
        /// If any error was produced during the execution of the activity, it will be stored here
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// List of executions tries for this activity
        /// </summary>
        public List<ActivityExecution> Executions { get; set; }
    }

    /// <summary>
    /// Represents a try of an activity
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
        /// Calculated moment for when this activity try should be considered as expired
        /// </summary>
        public DateTime ActivityTrySLAMoment { get; set; }

        /// <summary>
        /// Amount of time that the activity try expired by, this value is calculated by the monitor and doesn't need to be persited
        /// </summary>
        public TimeSpan? ActivityTrySLAExpiredBy { get; set; }

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
