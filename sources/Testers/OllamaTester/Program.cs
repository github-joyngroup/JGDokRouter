// See https://aka.ms/new-console-template for more information
using OllamaSharp;
using System.Text;

Console.WriteLine("Hello, World!");

// set up the client
var uri = new Uri("http://jgllm01.infosistema.com:11435/");
var ollama = new OllamaApiClient(uri);


WriteInstructions();

bool bTerminate = false;
while (!bTerminate)
{
    var line = Console.ReadLine();
    var lowerLine = line.ToLower().Trim();

    switch (lowerLine)
    {
        case "h":
        case "help":
            WriteInstructions();
            break;

        case "":
        case "exit":
            bTerminate = true;
            break;

        case "model":
            await StartModelManagement();
            break;

        case "chat":
            await StartChatInteraction();
            break;

        default:
            Console.WriteLine("Unknown command. Type h or help for help.");
            break;
    }
}

Console.WriteLine("Terminating, press enter to exit...");
Console.ReadLine();

void WriteInstructions()
{
    Console.Clear();
    Console.WriteLine("This is a test of the OllamaSharp API.");
    Console.WriteLine("h or help - show this help message");
    Console.WriteLine("model - change mode to Codel management");
    Console.WriteLine("chat - change mode to Chat interaction");
    Console.WriteLine("exit or an empty instruction will terminate the program");
}

async Task<bool> StartModelManagement()
{
    WriteModelManagementInstructions();
    bool bTerminate = false;
    while (!bTerminate)
    {
        var line = Console.ReadLine();
        var lowerLine = line.ToLower().Trim();
        var lineParts = lowerLine.Split(' ');
        var instruction = lineParts.FirstOrDefault() ?? "";
        switch (instruction)
        {
            case "h":
            case "help":
                WriteModelManagementInstructions();
                break;

            case "list":
                var models = await ollama.ListLocalModels();
                Console.WriteLine($"Found {models.Count()} Local models");
                foreach(var model in models)
                {
                    Console.WriteLine($" - {model.Name}");
                }
                Console.WriteLine();
                break;

            case "pull":
                if(lineParts.Length <= 1)
                {
                    Console.WriteLine("Use pull <model name>");
                    break;
                }
                var modelName = lineParts[1];
                //await ollama.PullModel(modelName, status => Console.WriteLine($"({status.Percent}%) {status.Status}"));
                await ollama.PullModel(modelName, status => UpdateProgressBar(status.Percent, status.Status));
                Console.WriteLine();
                break;

            case "selected":
                var selectedModel = ollama.SelectedModel;
                if(String.IsNullOrWhiteSpace(selectedModel))
                {
                    Console.WriteLine("No model selected. Use select <model name> to select a model.");
                    break;
                }
                else
                {
                    Console.WriteLine($"Selected model: {selectedModel}");
                }
                break;

            case "select":
                if(lineParts.Length <= 1)
                {
                    Console.WriteLine("Use select <model name>");
                    break;
                }
                var selectedModelName = lineParts[1];
                ollama.SelectedModel = selectedModelName;
                Console.WriteLine($"Model {ollama.SelectedModel} selected");
                break;

            case "":
            case "exit":
                bTerminate = true;
                WriteInstructions();
                break;

            default:
                Console.WriteLine("Unknown command. Type h or help for help.");
                break;
        }
    }

    return true;
}

void WriteModelManagementInstructions()
{
    Console.Clear();
    Console.WriteLine("Model Management");
    Console.WriteLine("h or help - show this help message");
    Console.WriteLine("list - list local models");
    Console.WriteLine("pull <model name> - pull a model from the server");
    Console.WriteLine("selected - show the selected model");
    Console.WriteLine("select <model name> - select a model");
    Console.WriteLine("exit or an empty instruction will go back to main");
}

async Task<bool> StartChatInteraction()
{
    if(String.IsNullOrWhiteSpace(ollama.SelectedModel))
    {
        Console.WriteLine("Chat interaction requires a model to be selected. Use model to select a model first.");
        return false;
    }

    ConversationContext context = null;

    WriteChatInteractionInstructions();
    bool bTerminate = false;
    while (!bTerminate)
    {
        var line = Console.ReadLine();
        var lowerLine = line.ToLower().Trim();
        var lineParts = lowerLine.Split(' ');

        switch (lowerLine)
        {
            case "h":
            case "help":
                WriteChatInteractionInstructions();
                break;

            case "":
            case "exit":
                bTerminate = true;
                WriteInstructions();
                break;

            default:
                try
                {
                    context = await ollama.StreamCompletion(line, context, stream => Console.Write(stream.Response));
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Error chatting with ollama: {ex.Message}");
                    Console.WriteLine($"Typical case is selected model invalid or not loaded ");
                }
                Console.WriteLine();
                break;
        }
    }

    return true;
}

void WriteChatInteractionInstructions()
{
    Console.Clear();
    Console.WriteLine("Chat Interaction");
    Console.WriteLine("h or help - show this help message");
    Console.WriteLine("exit or an empty instruction will go back to main");
    Console.WriteLine("Any other input will be sent to the chatbot.");
}

static void UpdateProgressBar(double percent, string status)
{
    const int totalBlocks = 50; // Total number of blocks in the progress bar
    int filledBlocks = ((int)percent * totalBlocks) / 100;
    int emptyBlocks = totalBlocks - filledBlocks;

    StringBuilder progressBar = new StringBuilder();
    progressBar.Append('[');
    progressBar.Append(new string('=', filledBlocks));
    progressBar.Append(new string(' ', emptyBlocks));
    progressBar.Append(']');
    progressBar.Append($" {percent}% {status}");

    Console.Write($"\r{progressBar.ToString()}");

    if (percent == 100)
    {
        Console.WriteLine(); // Move to the next line when complete
    }
}