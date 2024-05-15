using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.Common.Models
{
    /// <summary>
    /// Represents a loaded activity configuration that is available in the activity pool to be used in the pipelines
    /// </summary>
    public class ActivityDefinition
    {
        public ActivityConfiguration Configuration { get; set; }

        /// <summary>
        /// For direct executions, the handler that should be called when the activity is executed
        /// </summary>
        public OnExecuteActivityHandler DirectActivityHandler { get; set; }

        /// <summary>
        /// For HTTP executions, the URL that should be called when the activity is executed
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// For kafka event executions, the topic that should be used to push the message
        /// </summary>
        public string KafkaTopic { get; set; }

        /// <summary>
        /// Common configurations that will be used by the activity definition - will have the engine level configurations merged with the activity configurations
        /// May be override with the pipeline level configurations
        /// </summary>
        public CommonConfigurations CommonConfigurations { get; set; }

    }
}