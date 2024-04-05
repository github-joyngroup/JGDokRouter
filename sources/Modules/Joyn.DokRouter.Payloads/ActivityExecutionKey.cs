using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.Payloads
{
    public class ActivityExecutionKey
    {
        public PipelineInstanceKey PipelineInstanceKey { get; set; }
        public Guid ActivityDefinitionIdentifier { get; set; }
        public Guid ActivityExecutionIdentifier { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is ActivityExecutionKey key)
            {
                return key.PipelineInstanceKey.Equals(this.PipelineInstanceKey) && 
                       key.ActivityDefinitionIdentifier == this.ActivityDefinitionIdentifier &&
                       key.ActivityExecutionIdentifier == this.ActivityExecutionIdentifier;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PipelineInstanceKey.GetHashCode(), ActivityDefinitionIdentifier, ActivityExecutionIdentifier);
        }
    }
}
