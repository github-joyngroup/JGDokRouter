using DocDigitizer.Common.Logging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Joyn.LLMDriver.HelperWorkers
{
    public class ChatGPTClient
    {
        private static readonly HttpClient HttpClient = new();

        /*Where we store all the conversations*/
        private static readonly ConcurrentDictionary<string, List<Message>> UserContexts = new();

        private static ChatGPTClientSettings Settings;
        private static ChatGPTClient Client;
        private static readonly object clientLocker = new();
        public static void Startup(ChatGPTClientSettings settings)
        {
            if (settings == null) { throw new ArgumentNullException(nameof(settings)); }

            lock (clientLocker)
            {
                Settings = settings;

                Client = new ChatGPTClient()
                {
                    ApiKey = settings.ApiKey,
                    ApiUrl = settings.ApiUrl,
                    Model = settings.Model
                };

                HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Client.ApiKey);
                HttpClient.Timeout = settings.TimeOutInSeconds.HasValue ? TimeSpan.FromSeconds(settings.TimeOutInSeconds.Value) : HttpClient.Timeout;
            }

            DDLogger.LogInfo<ChatGPTClient>($"ChatGPTClient started using model '{Settings.Model}' on API url '{Settings.ApiUrl}'");
        }

        private string ApiKey;
        private string ApiUrl;
        private string Model;

        /// <summary>
        /// Places a request to the ChatGPT API.
        /// If the user context is not found, it will be created, otherwise it will be updated.
        /// If any system messages are configured, they will be included in the context.
        /// </summary>
        /// <param name="userInput">the user prompt</param>
        /// <param name="userId">the user or context identifier</param>
        /// <returns>Returns the assistant response</returns>
        /// <exception cref="Exception">On error calling ChatGPT API</exception>
        public static async Task<string> PlaceRequest(string userInput, string userId)
        {
            if (Client == null) { throw new Exception("ChatGPTClient not initialized. Startup method should be invoked before first usage."); }
            var context = UserContexts.GetOrAdd(userId, new List<Message>());

            //Include configured system messages, if any and if not already present in the context
            if (Settings.SystemMessages.Any() && !context.Any(m => m.Role == ChatRoleType.System))
            {
                foreach (var systemMessage in Settings.SystemMessages)
                {
                    context.Add(new Message { Role = ChatRoleType.System, Content = systemMessage });
                }
            }

            //Prepare the messages to send to ChatGPT
            var messages = new List<dynamic> { };

            // Add the existing conversation history, if any, to the messages
            foreach (var message in context)
            {
                messages.Add(new { role = message.Role, content = message.Content });
            }

            // Add the user's input to the messages
            messages.Add(new { role = ChatRoleType.User, content = userInput });

            //Prepare the request body to send to ChatGPT
            var requestBody = new
            {
                messages,
                //max_tokens = Settings.MaxTokens ?? 1000,
                //n = GPTNParameter,
                //stop = "\n\n",
                //temperature = GPTTemperatureParameter,
                response_format = new { type = "json_object" },
                model = Client.Model
            };

            var requestID = Guid.NewGuid().ToString();
            //var jsonContent = JsonConvert.SerializeObject(requestBody);
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            DDLogger.LogDebug<ChatGPTClient>($"ChatGPTClient requestID '{requestID}' userId '{userId}', request:'{jsonContent}'");

            HttpResponseMessage response = null;
            try
            {
                response = await HttpClient.PostAsync(Client.ApiUrl, content);
                var jsonResponse = await response.Content.ReadAsStringAsync();

                DDLogger.LogDebug<ChatGPTClient>($"ChatGPTClient requestID '{requestID}' userId '{userId}', response:'{jsonResponse}'");

                //var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                //var responseObject = System.Text.Json.JsonSerializer.Deserialize<dynamic>(jsonResponse);

                Func<string, string> ExtractAnswer = (json) =>
                {
                    if(String.IsNullOrWhiteSpace(json)) { return String.Empty; }
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        JsonElement root = doc.RootElement;
                        JsonElement choices = root.GetProperty("choices");
                        JsonElement firstChoice = choices[0];
                        JsonElement message = firstChoice.GetProperty("message");
                        return message.GetProperty("content").GetString().Trim();
                    }
                };

                Func<string, string> ExtractError= (json) =>
                {
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
                    var answer = ExtractAnswer(jsonResponse);

                    if (Settings.IsContextPersisted)
                    {
                        // Update the context for the user
                        context.Add(new Message { Role = ChatRoleType.User, Content = userInput });
                        context.Add(new Message { Role = ChatRoleType.Assistant, Content = answer });
                    }

                    return answer;
                }

                throw new Exception($"Error connecting to ChatGPT API.\r\nResponse Code: {response.StatusCode}\r\n{ExtractError(jsonResponse)}");
            }
            catch (Exception ex)
            {
                //log.Error($"ChatGPTClient requestID '{requestID}' userId '{userId}'", ex);
                throw;
            }

        }

        public static void ClearContext(string userId)
        {
            UserContexts.TryRemove(userId, out _);
        }

        public static int GetConsumedTokens(string userInput, string userId)
        {
            int numberOfTokens = 0;
            var context = UserContexts.GetOrAdd(userId, new List<Message>());

            foreach (var message in context)
            {
                numberOfTokens += message.Content.Split(" ").Length;
            }

            return numberOfTokens;
        }

        public static int GetNumberOfTokens(string text)
        {
            return text.Split(" ").Length;
        }

        public static bool IsContextFull(string userInput, string userId)
        {
            var numberOfTokens = GetConsumedTokens(userInput, userId);
            return numberOfTokens >= Settings.MaxTokens;
        }

    }

    public class ChatGPTClientSettings
    {
        public string ApiKey { get; set; }
        public string ApiUrl { get; set; }
        public string Model { get; set; }
        public int? MaxTokens { get; set; }
        public List<string> SystemMessages { get; set; } = new List<string>();
        public int? TimeOutInSeconds { get; set; } = 120;
        public bool IsContextPersisted { get; set; } = false;
    }

    public class Message
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public static class ChatRoleType
    {
        public const string User = "user";
        public const string System = "system";
        public const string Assistant = "assistant";
    }
}
