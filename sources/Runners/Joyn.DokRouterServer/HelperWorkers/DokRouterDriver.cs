using Joyn.DokRouter.Common.Models;
using Joyn.DokRouter.Common.Payloads;
using System.Text;

namespace Joyn.DokRouterServer.HelperWorkers
{
    public class DokRouterDriver
    {
        private static readonly HttpClient HttpClient = new();
        private static readonly object clientLocker = new();

        private static string _endActivityCallbackUrl;

        public static void Startup(string endActivityCallbackUrl)
        {
            _endActivityCallbackUrl = endActivityCallbackUrl;
        }

        public static async void OnStartActivity(ActivityDefinition activityDefinition, StartActivityOut startActivityOutPayload)
        {
            //Fill Callback Url
            startActivityOutPayload.CallbackUrl = _endActivityCallbackUrl;

            switch (activityDefinition.Configuration.Kind)
            {
                case ActivityKind.Direct:
                    activityDefinition.DirectActivityHandler(startActivityOutPayload);
                    break;

                case ActivityKind.HTTP:
                    var jsonContent = System.Text.Json.JsonSerializer.Serialize(startActivityOutPayload);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    var response = await HttpClient.PostAsync(activityDefinition.Url, content);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    "0".ToString();
                    break;

                default:
                    throw new NotImplementedException($"Activity Kind {activityDefinition.Configuration.Kind} unknown or not implemented");
            }
        }

        public static async void ExecuteDirectActivity(StartActivityOut startActivityPayload)
        {
            Console.WriteLine("Executing Direct activity");
            Thread.Sleep(Random.Shared.Next(2000, 5000));
            Console.WriteLine("Finished Direct activity execution");

            EndActivity endActivityPayload = new EndActivity()
            {
                ActivityExecutionKey = startActivityPayload.ActivityExecutionKey,
                IsSuccess = true
            };
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(endActivityPayload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            //Callback after finishing
            Console.WriteLine($"Invoking {startActivityPayload.CallbackUrl} to flag end activity");
            var response = await HttpClient.PostAsync(startActivityPayload.CallbackUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
        }

        public static async void SetupCycleActivity(StartActivityOut startActivityPayload)
        {
            Console.WriteLine("Executing SetupCycle activity");
            Thread.Sleep(Random.Shared.Next(2000, 3000));
            var numberCycles = Random.Shared.Next(4, 20);
            Console.WriteLine($"Finished SetupCycle execution - will ask for {numberCycles} cycles");

            EndActivity endActivityPayload = new EndActivity()
            {
                ActivityExecutionKey = startActivityPayload.ActivityExecutionKey,
                IsSuccess = true,
                ProcessInstanceData = new Dictionary<string, string>()
                {
                    { "numberCycles", numberCycles.ToString() }
                }
            };
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(endActivityPayload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            //Callback after finishing
            Console.WriteLine($"Invoking {startActivityPayload.CallbackUrl} to flag end activity");
            var response = await HttpClient.PostAsync(startActivityPayload.CallbackUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
        }

        public static async void ExecuteCycleActivity(StartActivityOut startActivityPayload)
        {
            Console.WriteLine($"Executing Cycle activity #{startActivityPayload.ActivityExecutionKey.CycleCounter}");
            Thread.Sleep(Random.Shared.Next(1000, 1000));
            Console.WriteLine($"Finished Cycle activity #{startActivityPayload.ActivityExecutionKey.CycleCounter}");

            EndActivity endActivityPayload = new EndActivity()
            {
                ActivityExecutionKey = startActivityPayload.ActivityExecutionKey,
                IsSuccess = true
            };
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(endActivityPayload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            //Callback after finishing
            Console.WriteLine($"Invoking {startActivityPayload.CallbackUrl} to flag end activity");
            var response = await HttpClient.PostAsync(startActivityPayload.CallbackUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
        }
    }
}
