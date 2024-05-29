using DocDigitizer.Common.Logging;
using DocDigitizer.Common.WAPI.Filters;
using Joyn.LLMDriver.Controllers;
using Joyn.LLMDriver.DAL;
using Joyn.LLMDriver.HelperWorkers;
using Microsoft.Extensions.Logging;

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

//Filters
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<LogExceptionAttribute>();
});

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<MainLLMDriver>>();
DDLogger.Startup(logger);

try
{
    logger.LogInformation("Running Startup for services");
    logger.LogDebug("Starting up Timelog Client");

    var timeloggerConfiguration = app.Configuration.GetSection("TimelogClient").Get<Joyn.Timelog.Client.LoggerConfiguration>();
    logger.LogDebug($"LoggerConfiguration:\r\n{(timeloggerConfiguration != null ? System.Text.Json.JsonSerializer.Serialize(timeloggerConfiguration) : "NULL!")}");
    Joyn.Timelog.Client.Logger.Startup(Guid.Parse(app.Configuration["ApplicationKey"]), timeloggerConfiguration, logger);
}
catch (Exception ex)
{
    DDLogger.LogException<Program>("Exception when starting up Timelog Client. Will continue without timelog capabilities", ex);
}

try
{
    System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", app.Configuration["GoogleApplicationCredentialsFilepath"]);

    //Start specific services here
    FileWorker.Startup(app.Configuration["NRecoLicenseOwner"], app.Configuration["NRecoLicenseKey"]);
    BaseMongoMapper.Startup(app.Configuration["MongoConnection"], app.Configuration["MongoDatabaseName"]);
    DomainController.Startup(app.Configuration.GetSection("DomainController").Get<DomainControllerConfiguration>());
    DomainWorker.Startup(app.Configuration.GetSection("DomainWorker").Get<DomainWorkerConfiguration>());
    ResumatorWorker.Startup(app.Configuration.GetSection("ResumatorWorker").Get<ResumatorWorkerConfiguration>());
    LLMWorker.Startup(app.Configuration["ChatGPTPromptsLocation"]);
    ChatGPTClient.Startup(app.Configuration.GetSection("ChatGPTClient").Get<ChatGPTClientSettings>());
    BizapisClient.Startup(app.Configuration.GetSection("BizapisClient").Get<BizapisClientConfiguration>());
}
catch (Exception ex)
{
    DDLogger.LogException<Program>("Unable to startup Program. Will Quit.", ex);
    System.Threading.Thread.Sleep(5000);
    Console.WriteLine("Press any key to exit");
    Console.ReadLine();
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
class MainLLMDriver { }