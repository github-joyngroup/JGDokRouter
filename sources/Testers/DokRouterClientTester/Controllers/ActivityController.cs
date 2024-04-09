using Joyn.DokRouter.Payloads;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;

namespace DokRouterClientTester.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ActivityController : ControllerBase
    {
        private static readonly HttpClient HttpClient = new();

        [HttpPost("StartActivity1")]
        public IActionResult StartActivity1(StartActivityOut startActivityPayload)
        {
            "0".ToString();
            //Do something async
            Task.Run(async () =>
            {
                if (!startActivityPayload.TestMode)
                {
                    Console.WriteLine("Executing HTTP activity: Activity1");
                    Thread.Sleep(Random.Shared.Next(2000, 5000));
                    Console.WriteLine("Finished HTTP activity: Activity1");
                }

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
            });

            return Ok();
        }

        //[HttpGet(Name = "StartActivity2")]
        //public IActionResult StartActivity2(ActivityExecutionKey activityExecutionKey, object externalData)
        //{
        //    return Ok();
        //}
    }
}
