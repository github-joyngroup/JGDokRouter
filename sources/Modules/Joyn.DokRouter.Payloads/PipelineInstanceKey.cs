using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.Payloads
{
    public class PipelineInstanceKey
    {
        public Guid PipelineDefinitionIdentifier { get; set; }
        public Guid PipelineInstanceIdentifier { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is PipelineInstanceKey key)
            {
                return key.PipelineDefinitionIdentifier == this.PipelineDefinitionIdentifier && key.PipelineInstanceIdentifier == this.PipelineInstanceIdentifier;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PipelineDefinitionIdentifier, PipelineInstanceIdentifier);
        }
    }
}
