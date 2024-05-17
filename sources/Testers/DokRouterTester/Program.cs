using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Configuration;
using Microsoft.Extensions.Configuration;
using DocDigitizer.Common.Logging;
using Joyn.DokRouter;
using Joyn.DokRouter.Common.Payloads;
using DokRouterTester.SamplePipeline;
using Joyn.DokRouter.Common.Models;
using Joyn.DokRouter.Common.Payloads;
using Joyn.DokRouter.Common.DAL;
using Joyn.DokRouter.DAL;
using Google.Protobuf.WellKnownTypes;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureAppConfiguration((hostContext, config) =>
{
    config.AddJsonFile("appsettings.json", optional: true);
});

builder.ConfigureServices((hostContext, services) =>
{
    services.AddLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddFile(options => { hostContext.Configuration.GetSection("Logging:File").Bind(options); }); //Requires nuget NetEscapades.Extensions.Logging.RollingFile
    });
});

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<MainTestAPI>>();

DDLogger.Startup(logger);
try
{
    //Check if  host.Services.GetService<IConfiguration>()["EndActivityCallbackUrl"]) works
    Joyn.DokRouter.MainEngine.Startup(new MockDokRouterDAL(), host.Services.GetService<IConfiguration>()["EndActivityCallbackUrl"]);
}
catch (Exception ex)
{
    DDLogger.LogException<Program>("Unable to startup Program. Will Quit.", ex);
    System.Threading.Thread.Sleep(5000);
    return;
}
DDLogger.LogInfo<Program>("Starting...");

DDLogger.LogInfo<Program>("Runnning...");

#if DEBUG
var hostTask = host.RunAsync();

HelperViewer.WriteInstructions();

var readConsole = "";
do
{
    readConsole = Console.ReadLine();
    if (readConsole == "startmany")
    {
        for (var i = 0; i < 25; i++)
        {
            Task.Run(() =>
            {
                MainEngine.StartPipeline(new StartPipeline()
                {
                    PipelineDefinitionIdentifier = null,
                    //MarshalledExternalData = Guid.NewGuid().ToString()
                });
            });
        }
    }
    else if (readConsole.StartsWith("start"))
    {
        var pipelineGuid = Guid.Empty;
        if (readConsole.Length > 5)
        {
            var pipelineGuidString = readConsole.Substring(6).Trim();
            if (!Guid.TryParse(pipelineGuidString, out pipelineGuid))
            {
                Console.WriteLine($"Invalid Guid: {pipelineGuidString}");
                continue;
            }
        }

        MainEngine.StartPipeline(new StartPipeline()
        {
            PipelineDefinitionIdentifier = pipelineGuid != Guid.Empty ? pipelineGuid : (Guid?)null,
            //MarshalledExternalData = Guid.NewGuid().ToString()
        });

        Console.WriteLine($"Started pipeline...");
    }
    else if (readConsole == "beginstate")
    {
        HelperViewer.BeginState();
    }
    else if(readConsole == "stopstate")
    {
        HelperViewer.EndState();
        Console.WriteLine("Stopped viewer...");
    }
    else if (readConsole != null && readConsole.ToLower().Trim() == "help" || readConsole != null && readConsole.ToLower().Trim() == "h") { HelperViewer.WriteInstructions(); }

} while (readConsole != "" && readConsole != null); //Empty string will terminate the program

#else
host.Run();
#endif

DDLogger.LogInfo<Program>("Terminated.");

/******************************************************************/
//Just for nice Log Structure
public class MainTestAPI { }

static class HelperViewer
{
    public static void WriteInstructions()
    {
        Console.Clear();
        Console.WriteLine("Write Command to execute");
        Console.WriteLine("start <guid> = Start pipeline defined by <guid>. if <guid> empty starts the default pipeline");
        Console.WriteLine("startmany = Start 25 parallel pipelines");
        Console.WriteLine("beginstate = Start state viewer");
        Console.WriteLine("stopstate = Stop state viewer");
        Console.WriteLine("help or h = Clears console and writes this instructions again");

        Console.WriteLine("Press Enter to send...");
        Console.WriteLine("An empty string will terminate the program.");
     
        Console.WriteLine();
    }

    private static bool stateOn = false;
    public static void BeginState()
    {
        Task.Run(() =>
        {
            //Discontinued
            //stateOn = true;
            //while (stateOn)
            //{
            //    var stateData = MainEngine.GetState();
            //    Console.Clear();
            //    Console.WriteLine("Running Instances:");
            //    foreach (var item in stateData)
            //    {
            //        Console.WriteLine($"Pipeline: {item.PipelineDefinitionIdentifier} - has {item.PipelineInstances.Count} Running instances");
            //        foreach (var instance in item.PipelineInstances)
            //        {
            //            Console.WriteLine($" Definition: {instance.Key.PipelineDefinitionIdentifier} - Instance: {instance.Key.PipelineInstanceIdentifier} - StartedAt: {instance.StartedAt} - CurrentActivityIndex: {instance.CurrentActivityIndex}");
            //            foreach (var activityIndex in instance.ActivityExecutions.Keys.OrderBy(k => k))
            //            {
            //                var executions = instance.ActivityExecutions[activityIndex];
            //                foreach (var execution in executions)
            //                {
            //                    Console.WriteLine($"    Activity Execution [{activityIndex}]: Definition: {execution.Value.Key.ActivityDefinitionIdentifier} - Execution: {execution.Value.Key.ActivityExecutionIdentifier} - StartedAt: {execution.Value.StartedAt} - EndedAt: {execution.Value.EndedAt} - IsSuccess: {execution.Value.IsSuccess}");
            //                }
            //            }
            //        }
            //    }

            //    Console.WriteLine(); 
            //    Console.WriteLine("".PadLeft(50, '-'));
            //    Console.WriteLine("stopstate - to stop viewer");
            //    Console.WriteLine();

            //    Thread.Sleep(1500);
            //}
        });
    }

    public static void EndState()
    {
        stateOn = false;
    }   

}

static class HelperEventTriggering
{
    public static void StartTestActivity1(ActivityExecutionKey activityExecutionKey, object externalData)
    {
        Task.Run(() => TestActivity1.Execute(activityExecutionKey, externalData));
    }

    public static void StartTestActivity2(ActivityExecutionKey activityExecutionKey, object externalData)
    {
        Task.Run(() => TestActivity2.Execute(activityExecutionKey, externalData));
    }

    public static void OnEndActivity(EndActivity endActivityPayload)
    {
        Task.Run(() => MainEngine.EndActivity(endActivityPayload));
    }
}