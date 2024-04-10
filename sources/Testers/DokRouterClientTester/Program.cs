using DocDigitizer.Common.Logging;
using DocDigitizer.Common.LogShipping;
using DocDigitizer.Common.WAPI.Filters;
using DokRouterClientTester.HelperWorkers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Configuration.AddJsonFile("appsettings.json");

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddFile(options => { builder.Configuration.GetSection("Logging:File").Bind(options); }); //Requires nuget NetEscapades.Extensions.Logging.RollingFile
});

builder.Services.AddSingleton<FileSystemWatcherServiceConfiguration>((options =>
{
    var fileSystemWatcherServiceConfiguration = new FileSystemWatcherServiceConfiguration();
    builder.Configuration.GetSection("FileSystemWatcher").Bind(fileSystemWatcherServiceConfiguration);
    return fileSystemWatcherServiceConfiguration;
}));

builder.Services.AddSingleton<ChatGPTClientSettings>((options =>
{
    var chatGPTClientSettings = new ChatGPTClientSettings();
    builder.Configuration.GetSection("ChatGPTClientSettings").Bind(chatGPTClientSettings);
    return chatGPTClientSettings;
})); 

builder.Services.AddHostedService<FileSystemWatcherService>();

//Filters
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<LogExceptionAttribute>();
});


var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<MainDokRouterClientTester>>();
DDLogger.Startup(logger);

try
{
    System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", app.Configuration["GoogleApplicationCredentialsFilepath"]);

    DDLLMClonePipeline.Startup(app.Configuration["NRecoLicenseOwner"], app.Configuration["NRecoLicenseKey"], app.Configuration["ChatGPTDOcumentClassesPromptsLocation"]);
    FileSystemWatcherService.Startup(app.Services.GetRequiredService<FileSystemWatcherServiceConfiguration>(), logger);
    ChatGPTClient.Startup(app.Services.GetRequiredService<ChatGPTClientSettings>());
}
catch (Exception ex)
{
    DDLogger.LogException<Program>("Unable to startup Program. Will Quit.", ex);
    System.Threading.Thread.Sleep(5000);
    return;
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

/******************************************************************/
//Just for nice Log Structure
class MainDokRouterClientTester { }