using DocDigitizer.Common.Logging;
using Joyn.DokRouter.Common.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Joyn.LLMDriver.HelperWorkers
{
    public class BizapisClient
    {
        private static readonly HttpClient httpClient = new();

        private static BizapisClientConfiguration _configuration;

        public static void Startup(BizapisClientConfiguration configuration)
        {
            _configuration = configuration;
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", configuration.BearerToken);
        }
        
        /// <summary>
        /// Will fetch a list of Resumator Documents for a given email, apply_date and portal
        /// </summary>
        /// <param name="email">The applicant Email</param>
        /// <param name="apply_date">The application moment</param>
        /// <param name="portal">Company identifier</param>
        public static async Task<List<ResumatorDocument>> GetResumatorDocuments(string email, string apply_date, string portal)
        {
            //Prepare the request body to send to ChatGPT
            var requestBody = new
            {
                email = email,
                apply_date = apply_date,
                portal = portal
            };

            var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            DDLogger.LogDebug<BizapisClient>($"BizapisClient request:'{jsonContent}'");

            try
            {
                var response = await httpClient.PostAsync(_configuration.GetResumatorDocumentsUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                DDLogger.LogDebug<ChatGPTClient>($"BizapisClient response status: {response.StatusCode} length: {responseContent}");

                Func<string, List<ResumatorDocument>> ExtractAnswer = (json) =>
                {
                    List<ResumatorDocument> retList = new List<ResumatorDocument>();

                    if (String.IsNullOrWhiteSpace(json)) { return retList; }
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        JsonElement root = doc.RootElement;
                        JsonElement files = root.GetProperty("files");
                        for(var idx = 0; idx < files.GetArrayLength(); idx++)
                        {
                            retList.Add(new ResumatorDocument()
                            {
                                FileName = files[idx].GetProperty("filename").GetString().Trim(),
                                ContentType = files[idx].GetProperty("content_type").GetString().Trim(),
                                Content = Convert.FromBase64String(files[idx].GetProperty("content").GetString().Trim())
                            });
                        }
                    }

                    return retList;
                };

                Func<string, string> ExtractError= (json) =>
                {
                    ///WILL BIZAPIS ERRO HAVE THIS STRUCTURE?
                    if (String.IsNullOrWhiteSpace(json)) { return String.Empty; }
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        JsonElement root = doc.RootElement;
                        JsonElement error = root.GetProperty("error");
                        return error.GetProperty("message").GetString().Trim();
                    }
                };

                if (response.IsSuccessStatusCode)
                {
                    //responseObject.choices[0].message.content.ToString().Trim()
                    return ExtractAnswer(responseContent);
                }

                throw new Exception($"Error connecting to ChatGPT API.\r\nResponse Code: {response.StatusCode}\r\n{ExtractError(responseContent)}");
            }
            catch (Exception ex)
            {
                //log.Error($"ChatGPTClient requestID '{requestID}' userId '{userId}'", ex);
                throw;
            }

        }
    }

    public class BizapisClientConfiguration
    {
        public string GetResumatorDocumentsUrl { get; set; }
        public string BearerToken { get; set; }
    }

    public class ResumatorDocument
    {
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public byte[] Content { get; set; }
    }
}
