using Joyn.DokRouter.Common.Payloads;
using System.Text;

namespace Joyn.LLMDriver.HelperWorkers
{
    public class Common
    {

        private static readonly HttpClient HttpClient = new(new HttpClientHandler()
        {
#if DEBUG
            //Note: This is a workaround to bypass SSL certificate validation - NOT SUITABLE FOR PRODUCTION
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }
#endif
        });

        public static async Task StartPipeline(Guid pipelineIdentifierToStart, Guid transactionIdentifier, byte[] marshalledExternalData, string startPipelineUrl)
        {
            try
            {
                StartPipeline startPipelinePayload = new StartPipeline()
                {
                    PipelineDefinitionIdentifier = pipelineIdentifierToStart,
                    TransactionIdentifier = transactionIdentifier,
                    MarshalledExternalData = marshalledExternalData
                };

                var jsonContent = System.Text.Json.JsonSerializer.Serialize(startPipelinePayload);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                //Callback after finishing
                Console.WriteLine($"Invoking {startPipelineUrl} to start pipeline {pipelineIdentifierToStart}");
                var response = await HttpClient.PostAsync(startPipelineUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public static async void CallbackEndActivity(StartActivityOut startActivityPayload, byte[] marshalledExternalData, bool isSuccess, string errorMessage)
        {
            EndActivity endActivityPayload = new EndActivity()
            {
                ActivityExecutionKey = startActivityPayload.ActivityExecutionKey,
                IsSuccess = isSuccess,
                ErrorMessage = errorMessage,
                MarshalledExternalData = marshalledExternalData
            };
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(endActivityPayload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            //Callback after finishing
            Console.WriteLine($"Invoking {startActivityPayload.CallbackUrl} to flag end of activity {endActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier}");
            var response = await HttpClient.PostAsync(startActivityPayload.CallbackUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            "0".ToString();
        }
    }
}
