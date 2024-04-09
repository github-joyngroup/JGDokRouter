using DocDigitizer.Common.Logging;
using Joyn.DokRouter;
using Joyn.DokRouter.Models;
using Joyn.DokRouter.Payloads;
using Newtonsoft.Json;
using System.Text;

namespace DokRouterServerTester.HelperWorkers
{
    public class DokRouterDriver
    {
        private static readonly HttpClient HttpClient = new();
        private static readonly object clientLocker = new();

        public static async void OnStartActivity(ActivityExecutionDefinition activityExecutionDefinition, StartActivityOut startActivityOutPayload)
        {
            //Fill Callback Url
            startActivityOutPayload.CallbackUrl = "https://localhost:7285/DokRouter/EndActivity"; //TEST ONLY CANNOT BE HARDCODED HERE
            
            
            "0".ToString();
            switch(activityExecutionDefinition.Kind)
            {
                case ActivityKind.Direct:
                    activityExecutionDefinition.DirectActivityHandler(startActivityOutPayload);
                    break;

                case ActivityKind.HTTP:
                    var jsonContent = JsonConvert.SerializeObject(startActivityOutPayload);
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
            var jsonContent = JsonConvert.SerializeObject(endActivityPayload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            //Callback after finishing
            Console.WriteLine($"Invoking {startActivityPayload.CallbackUrl} to flag end activity");
            var response = await HttpClient.PostAsync(startActivityPayload.CallbackUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            "0".ToString();
        }

        //public static void StartTestActivity1(StartActivityOut startActivityOutPayload)
        //{
        //    "0".ToString();
        //    Task.Run(() =>
        //    {
        //        HttpResponseMessage response = null;
        //        try
        //        {
        //            //Prepare the request body to send to DokRouterClientTester
        //            startActivityOutPayload.CallbackUrl = "https://localhost:7285/DokRouter/EndActivity"; //TEST ONLY CANNOT BE HARDCODED HERE

        //            var jsonContent = JsonConvert.SerializeObject(startActivityOutPayload);
        //            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        //            var responseTask = HttpClient.PostAsync(_dokRouterClientBaseUrl + "/Activity/StartActivity1", content);
        //            responseTask.Wait();
        //            response = responseTask.Result;
        //            var jsonResponseTask = response.Content.ReadAsStringAsync();
        //            jsonResponseTask.Wait();
        //            var jsonResponse = jsonResponseTask.Result;

        //            DDLogger.LogInfo<DokRouterDriver>($"Requested Execution of Activity1', response:'{jsonResponse}'");

        //            var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

        //            if (response.IsSuccessStatusCode)
        //            {
        //                "0".ToString();
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            throw;
        //        }
        //    });
        //}
    }
}
