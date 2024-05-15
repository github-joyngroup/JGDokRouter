using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.Common.Models
{
    /// <summary>
    /// Represents a loaded pipeline configuration that is available in the pipeline pool to be used
    /// </summary>
    public class PipelineDefinition
    {
        public PipelineConfiguration Configuration { get; set; }

        /// <summary>
        /// Common configurations that will be used by the pipeline definition - will have the engine level configurations merged with the pipeline configurations
        /// </summary>
        public CommonConfigurations CommonConfigurations { get; set; }
    }
}