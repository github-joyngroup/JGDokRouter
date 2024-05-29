using DocDigitizer.Common.Logging;
using DocDigitizer.Common.Randomness;
using Joyn.DokRouter.Common.Models;
using Joyn.DokRouter.Common.Payloads;
using System.Text;

namespace Joyn.DokRouterServer.HelperWorkers
{
    public class SampleActivities
    {
        private const bool ChaosMode = true; //If true, the controller will randomly delay and fail tasks;
        private const double ProbabilityToDelay = 0.0; //Chance that a task will take more than 5 seconds;
        private const double ProbabilityToFail = 0.33; //Chance that a task will fail silently;

        private static readonly HttpClient HttpClient = new();

        public static async void ExecuteDirectActivity(StartActivityOut startActivityPayload)
        {
            if (!CheckChaosMode(startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier.ToString())) { return; }

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
            if (!CheckChaosMode(startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier.ToString())) { return; }

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
            if (!CheckChaosMode(startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier.ToString())) { return; }

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

        private static bool CheckChaosMode(string identifier)
        {
            if (ChaosMode)
            {
                if (TokenGeneration.GetInProbability(ProbabilityToDelay)) { DDLogger.LogInfo<SampleActivities>($"CHAOS DELAY for: {identifier}"); System.Threading.Thread.Sleep(5000); }
                else if (TokenGeneration.GetInProbability(ProbabilityToFail)) { DDLogger.LogInfo<SampleActivities>($"CHAOS FAIL for: {identifier}"); return false; }
            }

            return true;
        }
    }
}
