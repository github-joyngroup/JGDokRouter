using DocDigitizer.Common.Logging;
using DocDigitizer.Common.WAPI.Filters;
using DokRouterServerTester.HelperWorkers;
using Joyn.DokRouter;
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

builder.Services.AddSingleton<DokRouterEngineConfiguration>((options =>
{
    var dokRouterEngineConfiguration = new DokRouterEngineConfiguration();
    builder.Configuration.GetSection("DokRouter").Bind(dokRouterEngineConfiguration);
    return dokRouterEngineConfiguration;
}));


//Filters
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<LogExceptionAttribute>();
});


var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<MainDokRouterServerTester>>();
DDLogger.Startup(logger);

try
{
    Joyn.DokRouter.MainEngine.Startup(app.Services.GetRequiredService<DokRouterEngineConfiguration>());
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
class MainDokRouterServerTester { }