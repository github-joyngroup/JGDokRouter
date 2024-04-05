using Joyn.DokRouter.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.Models
{
    public class PipelineInstance
    {
        public PipelineInstanceKey Key { get; set; }

        public int CurrentActivityIndex { get; set; }
        public DateTime StartedAt { get; set; }

        public object ExternalData { get; set; }

        public Dictionary<int, Dictionary<ActivityExecutionKey, ActivityExecution>> ActivityExecutions { get; set; }
    }

    public class ActivityExecution
    {
        public ActivityExecutionKey Key { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public bool? IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
    }
}
