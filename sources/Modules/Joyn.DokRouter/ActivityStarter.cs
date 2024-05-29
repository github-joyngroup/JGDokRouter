using DocDigitizer.Common.Logging;
using Joyn.DokRouter.Common.Models;
using Joyn.DokRouter.Common.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter
{
    internal class ActivityStarter
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
                    if (!response.IsSuccessStatusCode)
                    {
                        DDLogger.LogError<ActivityStarter>($"Invocation to {activityDefinition.Url} was not successfull! Status Code received: {response.StatusCode}. Content:\r\n{responseContent}");
                    }
                    break;

                default:
                    throw new NotImplementedException($"Activity Kind {activityDefinition.Configuration.Kind} unknown or not implemented");
            }
        }
    }
}
