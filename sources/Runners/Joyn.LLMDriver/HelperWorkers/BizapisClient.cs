using DocDigitizer.Common.Logging;
using Joyn.DokRouter.Common.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using static Google.Rpc.Context.AttributeContext.Types;

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
        public static async Task<List<ResumatorDocument>> GetResumatorDocuments(string unified_search, string apply_date, string portal)
        {
            //Prepare the request body to send to ChatGPT
            var requestBody = new
            {
                unified_search = unified_search,
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

                DDLogger.LogDebug<ChatGPTClient>($"BizapisClient response status: {response.StatusCode} length: {responseContent.Length}");

                Func<string, List<ResumatorDocument>> ExtractAnswer = (json) =>
                {
                    List<ResumatorDocument> retList = new List<ResumatorDocument>();

                    if (String.IsNullOrWhiteSpace(json)) { return retList; }
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        JsonElement root = doc.RootElement;
                        JsonElement dataElm;
                        JsonElement errorElm;
                        JsonElement filesElm;
                        if (root.TryGetProperty("data", out dataElm))
                        {
                            if (dataElm.TryGetProperty("error", out errorElm))
                            {
                                DDLogger.LogError<BizapisClient>($"Error connecting to BIZAPIS API.\r\nResponse Code: {response.StatusCode}\r\n{errorElm.GetString().Trim()}");
                                return new List<ResumatorDocument>();
                            }
                            if (dataElm.TryGetProperty("files", out filesElm))
                            {
                                for (var idx = 0; idx < filesElm.GetArrayLength(); idx++)
                                {
                                    retList.Add(new ResumatorDocument()
                                    {
                                        FileName = filesElm[idx].GetProperty("fileName").GetString().Trim(),
                                        ContentType = filesElm[idx].GetProperty("content_type").GetString().Trim(),
                                        Content = Convert.FromBase64String(filesElm[idx].GetProperty("content").GetString().Trim())
                                    });
                                }
                            }
                        }
                        else if (root.TryGetProperty("error", out errorElm))
                        {
                            DDLogger.LogError<BizapisClient>($"Error connecting to BIZAPIS API.\r\nResponse Code: {response.StatusCode}\r\n{errorElm.GetString().Trim()}");
                            return new List<ResumatorDocument>();
                        }
                        else if (root.TryGetProperty("files", out filesElm))
                        {
                            for (var idx = 0; idx < filesElm.GetArrayLength(); idx++)
                            {
                                retList.Add(new ResumatorDocument()
                                {
                                    FileName = filesElm[idx].GetProperty("filename").GetString().Trim(),
                                    ContentType = filesElm[idx].GetProperty("content_type").GetString().Trim(),
                                    Content = Convert.FromBase64String(filesElm[idx].GetProperty("content").GetString().Trim())
                                });
                            }
                        }
                        else
                        {
                            DDLogger.LogWarn<BizapisClient>($"BizapisClient response did not contain files for: unified_search: '{unified_search}', apply_date: '{apply_date}', portal: '{portal}'");
                            DDLogger.LogWarn<BizapisClient>($"BizapisClient response:\r\n{json}");
                        }
                    }

                    return retList;
                };

                Func<string, string> ExtractError = (json) =>
                {
                    ///WILL BIZAPIS ERRO HAVE THIS STRUCTURE?
                    if (String.IsNullOrWhiteSpace(json)) { return String.Empty; }
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            JsonElement root = doc.RootElement;
                            return root.GetProperty("message").GetString().Trim();
                        }
                    }
                    catch
                    {
                        return "Response is not Json or does not have the expected format";
                    }
                };

                if (response.IsSuccessStatusCode)
                {
                    //responseObject.choices[0].message.content.ToString().Trim()
                    return ExtractAnswer(responseContent);
                }

                DDLogger.LogError<BizapisClient>($"Error connecting to BIZAPIS API.\r\nResponse Code: {response.StatusCode}\r\n{ExtractError(responseContent)}");
            }
            catch (HttpRequestException httpEx)
            {
                DDLogger.LogError<BizapisClient>($"Error obtaining response from BIZAPIS API: {httpEx.Message}{(httpEx.InnerException != null ? $"\r\n{httpEx.InnerException.Message}":"")}");
            }
            catch (Exception ex)
            {
                //log.Error($"ChatGPTClient requestID '{requestID}' userId '{userId}'", ex);
                throw;
            }

            return new List<ResumatorDocument>();
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
