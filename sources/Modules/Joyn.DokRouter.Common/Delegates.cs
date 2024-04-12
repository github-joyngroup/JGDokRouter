using Joyn.DokRouter.Common.Models;
using Joyn.DokRouter.Common.Payloads;

namespace Joyn.DokRouter.Common
{
    /// <summary>
    /// Delegate for the event that is triggered when an activity is started within the engine
    /// </summary>
    /// <param name="activityExecutionDefinition">The definition of the activity being executed so that the handler knows how to execute it</param>
    /// <param name="startActivityOutPayload">Information regarding the data associated to the activity being executed - it's identifer and marshalled external data</param>
    public delegate void OnStartActivityHandler(ActivityExecutionDefinition activityExecutionDefinition, StartActivityOut startActivityOutPayload);

    /// <summary>
    /// Delegate used by activities of Direct kind, will be called when the activity is executed
    /// </summary>
    /// <param name="startActivityOutPayload">Information regarding the data associated to the activity being executed - it's identifer and marshalled external data</param>
    public delegate void OnExecuteActivityHandler(StartActivityOut startActivityOutPayload);

}
