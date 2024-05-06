using Joyn.DokRouter.Common.Models;
using Joyn.DokRouter.Common.Payloads;
using System.Text;

namespace DokRouterServer.HelperWorkers
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

        public static async void OnStartActivity(ActivityExecutionDefinition activityExecutionDefinition, StartActivityOut startActivityOutPayload)
        {
            //Fill Callback Url
            startActivityOutPayload.CallbackUrl = _endActivityCallbackUrl;

            switch (activityExecutionDefinition.Kind)
            {
                case ActivityKind.Direct:
                    activityExecutionDefinition.DirectActivityHandler(startActivityOutPayload);
                    break;

                case ActivityKind.HTTP:
                    var jsonContent = System.Text.Json.JsonSerializer.Serialize(startActivityOutPayload);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    var response = await HttpClient.PostAsync(activityExecutionDefinition.Url, content);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    "0".ToString();
                    break;

                default:
                    throw new NotImplementedException($"Activity Kind {activityExecutionDefinition.Kind} unknown or not implemented");
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
    }
}
