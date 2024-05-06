using DocDigitizer.Common.Logging;
using DocDigitizer.Common.WAPI.Filters;
using Joyn.DokRouterServer.HelperWorkers;
using Joyn.DokRouter;
using Joyn.DokRouter.Common.DAL;
using Joyn.DokRouter.Common.Models;
using Joyn.DokRouter.DAL;
using Joyn.DokRouter.MongoDAL;
using Microsoft.Extensions.Logging;
using ProtoBuf.Meta;

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

builder.Services.AddHostedService<Joyn.DokRouter.EngineMonitor>();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<MainDokRouterServer>>();
DDLogger.Startup(logger);

try
{
    Joyn.Timelog.Client.Logger.Startup(Guid.Parse(app.Configuration["ApplicationKey"]), app.Configuration.GetSection("TimelogClient").Get<Joyn.Timelog.Client.LoggerConfiguration>(), logger);

    Joyn.DokRouter.MongoDAL.MainStorageHelper.Startup(app.Configuration["MongoConnection"], app.Configuration["MongoDatabaseName"]);
    DokRouterDriver.Startup(app.Configuration["EndActivityCallbackUrl"]);

    var dokRouterDAL = new DokRouterMongoDAL();

    Joyn.DokRouter.MainEngine.Startup(dokRouterDAL);
    Joyn.DokRouter.EngineMonitor.Startup(app.Configuration.GetSection("DokRouterMonitor").Get<Joyn.DokRouter.EngineMonitorConfiguration>(), dokRouterDAL, logger);
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
class MainDokRouterServer { }