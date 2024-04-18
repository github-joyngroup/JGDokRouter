namespace Joyn.DokRouter.Common.Models
{
    /// <summary>
    /// Represents configurations that might be placed on any level (Engine > Pipeline > Activity)
    /// The value to be applied by the engine will be the lowest level configuration that is not null. So configurations on the Activity Level will override configurations on the Pipeline Level, and configurations on the Pipeline Level will override configurations on the Engine Level.
    /// This Class will also hold the default configurations to be used when no configuration is specified
    /// </summary>
    public class CommonConfigurations
    {
        #region SLA Configuration

        /// <summary>
        /// Amount of time in seconds that the engine will wait for a pipeline to be executed before considering it as expired
        /// </summary>
        public int? PipelineSLATimeInSeconds { get; set; }

        /// <summary>
        /// Amount of time in seconds that the engine will wait for an activity to be executed before considering it as expired
        /// </summary>
        public int? ActivitySLATimeInSeconds { get; set; }

        /// <summary>
        /// Amount of time in seconds that the engine will wait for a each activity execution try before considering it as expired
        /// </summary>
        public int? ActivityTrySLATimeInSeconds { get; set; }

        /// <summary>
        /// Whether the engine should retry the activity when it expires
        /// </summary>
        public bool? RetryOnSLAExpired { get; set; }

        /// <summary>
        /// Maximum amount of retries that the engine will do when an activity expires
        /// </summary>
        public int? RetryOnSLAExpiredMaxRetries { get; set; }

        /// <summary>
        /// Amount of time in seconds that the engine will wait before retrying the activity when it expires
        /// </summary>
        public int? RetryOnSLAExpiredDelayInSeconds { get; set; }

        #endregion

        #region Error handling Configuration

        /// <summary>
        /// Whether the engine should retry the activity when it errors
        /// </summary>
        public bool? RetryOnError { get; set; }

        /// <summary>
        /// Maximum amount of retries that the engine will do when an activity errors
        /// </summary>
        public int? RetryOnErrorMaxRetries { get; set; }

        /// <summary>
        /// Amount of time in seconds that the engine will wait before retrying the activity that errored
        /// </summary>
        public int? RetryOnErrorDelayInSeconds { get; set; }

        #endregion

        public static CommonConfigurations DefaultCommonConfigurations = new CommonConfigurations()
        {
            PipelineSLATimeInSeconds = 600, //10 minutes

            ActivitySLATimeInSeconds = 30,
            ActivityTrySLATimeInSeconds = 10,
            RetryOnSLAExpired = true,
            RetryOnSLAExpiredMaxRetries = 5,
            RetryOnSLAExpiredDelayInSeconds = 3,

            RetryOnError = true,
            RetryOnErrorMaxRetries = 2,
            RetryOnErrorDelayInSeconds = 15
        };

        public CommonConfigurations Clone()
        {
            return new CommonConfigurations()
            {
                PipelineSLATimeInSeconds = this.PipelineSLATimeInSeconds,

                ActivitySLATimeInSeconds = this.ActivitySLATimeInSeconds,
                ActivityTrySLATimeInSeconds = this.ActivityTrySLATimeInSeconds,
                RetryOnSLAExpired = this.RetryOnSLAExpired,
                RetryOnSLAExpiredMaxRetries = this.RetryOnSLAExpiredMaxRetries,
                RetryOnSLAExpiredDelayInSeconds = this.RetryOnSLAExpiredDelayInSeconds,

                RetryOnError = this.RetryOnError,
                RetryOnErrorMaxRetries = this.RetryOnErrorMaxRetries,
                RetryOnErrorDelayInSeconds = this.RetryOnErrorDelayInSeconds
            };
        }

        public CommonConfigurations Override(CommonConfigurations otherCommonConfiguration)
        {
            if (otherCommonConfiguration == null) { return this; }

            PipelineSLATimeInSeconds = otherCommonConfiguration.PipelineSLATimeInSeconds ?? PipelineSLATimeInSeconds;
            ActivitySLATimeInSeconds = otherCommonConfiguration.ActivitySLATimeInSeconds ?? ActivitySLATimeInSeconds;
            ActivityTrySLATimeInSeconds = otherCommonConfiguration.ActivityTrySLATimeInSeconds ?? ActivityTrySLATimeInSeconds;
            RetryOnSLAExpired = otherCommonConfiguration.RetryOnSLAExpired ?? RetryOnSLAExpired;
            RetryOnSLAExpiredMaxRetries = otherCommonConfiguration.RetryOnSLAExpiredMaxRetries ?? RetryOnSLAExpiredMaxRetries;
            RetryOnSLAExpiredDelayInSeconds = otherCommonConfiguration.RetryOnSLAExpiredDelayInSeconds ?? RetryOnSLAExpiredDelayInSeconds;

            RetryOnError = otherCommonConfiguration.RetryOnError ?? RetryOnError;
            RetryOnErrorMaxRetries = otherCommonConfiguration.RetryOnErrorMaxRetries ?? RetryOnErrorMaxRetries;
            RetryOnErrorDelayInSeconds = otherCommonConfiguration.RetryOnErrorDelayInSeconds ?? RetryOnErrorDelayInSeconds;

            //Return this to allow chaining
            return this;
        }
    }
}
