using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

using TASagentTwitchBot.Core.Extensions;
using TASagentTwitchBot.Core.Web;

//Initialize DataManagement
BGC.IO.DataManagement.Initialize("TASagentBotDarkanine");

//
// Define and register services
//

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.WebHost
    .UseKestrel()
    .UseUrls("http://0.0.0.0:5000");

IMvcBuilder mvcBuilder = builder.Services.GetMvcBuilder();

//Register Core Controllers (with potential exclusions) 
mvcBuilder.RegisterControllersWithoutFeatures("Overlay", "Notifications", "Audio", "TTS");

//Add SignalR for Hubs
builder.Services.AddSignalR();

//Custom Database
builder.Services
    .AddDbContext<TASagentTwitchBot.Darkanine.Database.DatabaseContext>();

//Register custom database to be served for BaseDatabaseContext
builder.Services
    .AddScoped<TASagentTwitchBot.Core.Database.BaseDatabaseContext>(x => x.GetRequiredService<TASagentTwitchBot.Darkanine.Database.DatabaseContext>());

//Core Agnostic Systems
builder.Services
    .AddSingleton<TASagentTwitchBot.Core.Config.BotConfiguration>(TASagentTwitchBot.Core.Config.BotConfiguration.GetConfig())
    .AddSingleton<TASagentTwitchBot.Core.ICommunication, TASagentTwitchBot.Core.CommunicationHandler>()
    .AddSingleton<TASagentTwitchBot.Core.View.IConsoleOutput, TASagentTwitchBot.Core.View.BasicView>()
    .AddSingleton<TASagentTwitchBot.Core.ErrorHandler>()
    .AddSingleton<TASagentTwitchBot.Core.ApplicationManagement>()
    .AddSingleton<TASagentTwitchBot.Core.IMessageAccumulator, TASagentTwitchBot.Core.MessageAccumulator>();

//Custom Agnostic Systems
builder.Services
    .AddSingleton<TASagentTwitchBot.Core.IConfigurator, TASagentTwitchBot.Darkanine.Configurator>();


//Core Twitch Systems
builder.Services
    .AddSingleton<TASagentTwitchBot.Core.API.Twitch.HelixHelper>()
    .AddSingleton<TASagentTwitchBot.Core.API.Twitch.IBotTokenValidator, TASagentTwitchBot.Core.API.Twitch.BotTokenValidator>()
    .AddSingleton<TASagentTwitchBot.Core.API.Twitch.IBroadcasterTokenValidator, TASagentTwitchBot.Core.API.Twitch.BroadcasterTokenValidator>()
    .AddSingleton<TASagentTwitchBot.Core.Database.IUserHelper, TASagentTwitchBot.Core.Database.UserHelper>();

//Core Twitch Chat Systems
builder.Services
    .AddSingleton<TASagentTwitchBot.Core.IRC.IrcClient>()
    .AddSingleton<TASagentTwitchBot.Core.IRC.IIRCLogger, TASagentTwitchBot.Core.IRC.IRCLogger>()
    .AddSingleton<TASagentTwitchBot.Core.Chat.IChatMessageHandler, TASagentTwitchBot.Core.Chat.ChatMessageHandler>();

//Custom Twitch Chat Systems
builder.Services
    .AddSingleton<TASagentTwitchBot.Core.IRC.INoticeHandler, TASagentTwitchBot.Darkanine.IRC.IRCNoticeIgnorer>();

//Core PubSub System
builder.Services
    .AddSingleton<TASagentTwitchBot.Core.PubSub.PubSubClient>()
    .AddSingleton<TASagentTwitchBot.Core.PubSub.IRedemptionSystem, TASagentTwitchBot.Core.PubSub.RedemptionSystem>();

//Core Timer System
builder.Services.AddSingleton<TASagentTwitchBot.Core.Timer.ITimerManager, TASagentTwitchBot.Core.Timer.TimerManager>();

//Command System
//Core Commands
builder.Services
    .AddSingleton<TASagentTwitchBot.Core.Commands.CommandSystem>()
    .AddSingleton<TASagentTwitchBot.Core.Commands.ICommandContainer, TASagentTwitchBot.Core.Commands.SystemCommandSystem>()
    .AddSingleton<TASagentTwitchBot.Core.Commands.ICommandContainer, TASagentTwitchBot.Core.Commands.PermissionSystem>();

//Core Credit System
builder.Services
    .AddSingleton<TASagentTwitchBot.Core.Credit.ICreditManager, TASagentTwitchBot.Core.Credit.DisabledCreditManager>();


//XInput Services
builder.Services
    .AddSingleton<TASagentTwitchBot.Plugin.XInput.IButtonPressDispatcher, TASagentTwitchBot.Plugin.XInput.ButtonPressDispatcher>();

//Custom Chat Listener
builder.Services
    .AddSingleton<TASagentTwitchBot.Darkanine.ChatListener>();

//Routing
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
});


//
// Finished defining services
// Construct application
//

using WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.UseAuthorization();
app.UseDefaultFiles();

//Custom Web Assets
app.UseStaticFiles();

//Core Web Assets
app.UseCoreLibraryContent("TASagentTwitchBot.Core");

//Authentication Middleware
app.UseMiddleware<TASagentTwitchBot.Core.Web.Middleware.AuthCheckerMiddleware>();

//Map all Core Non-excluded controllers
app.MapControllers();

//Core Control Page Hub
app.MapHub<TASagentTwitchBot.Core.Web.Hubs.MonitorHub>("/Hubs/Monitor");



await app.StartAsync();

//
// Update Database with new migrations
//

using (IServiceScope serviceScope = app.Services.GetService<IServiceScopeFactory>()!.CreateScope())
{
    TASagentTwitchBot.Darkanine.Database.DatabaseContext context = serviceScope.ServiceProvider!.GetRequiredService<TASagentTwitchBot.Darkanine.Database.DatabaseContext>();
    context.Database.Migrate();
}

//
// Construct and run Configurator
//

TASagentTwitchBot.Core.ICommunication communication = app.Services.GetRequiredService<TASagentTwitchBot.Core.ICommunication>();
TASagentTwitchBot.Core.IConfigurator configurator = app.Services.GetRequiredService<TASagentTwitchBot.Core.IConfigurator>();

app.Services.GetRequiredService<TASagentTwitchBot.Core.View.IConsoleOutput>();

bool configurationSuccessful = await configurator.VerifyConfigured();

if (!configurationSuccessful)
{
    communication.SendErrorMessage($"Configuration unsuccessful.  Aborting.");

    await app.StopAsync();
    await Task.Delay(15_000);
    return;
}

//
// Construct required components and run
//
communication.SendDebugMessage("*** Starting Up Application ***");

TASagentTwitchBot.Core.ErrorHandler errorHandler = app.Services.GetRequiredService<TASagentTwitchBot.Core.ErrorHandler>();
TASagentTwitchBot.Core.ApplicationManagement applicationManagement = app.Services.GetRequiredService<TASagentTwitchBot.Core.ApplicationManagement>();

TASagentTwitchBot.Core.API.Twitch.IBotTokenValidator botTokenValidator = app.Services.GetRequiredService<TASagentTwitchBot.Core.API.Twitch.IBotTokenValidator>();
TASagentTwitchBot.Core.API.Twitch.IBroadcasterTokenValidator broadcasterTokenValidator = app.Services.GetRequiredService<TASagentTwitchBot.Core.API.Twitch.IBroadcasterTokenValidator>();

app.Services.GetRequiredService<TASagentTwitchBot.Core.Commands.CommandSystem>();
app.Services.GetRequiredService<TASagentTwitchBot.Core.IMessageAccumulator>();
app.Services.GetRequiredService<TASagentTwitchBot.Core.IRC.IrcClient>();
app.Services.GetRequiredService<TASagentTwitchBot.Core.PubSub.PubSubClient>();
app.Services.GetRequiredService<TASagentTwitchBot.Darkanine.ChatListener>();

//Kick off Validators
botTokenValidator.RunValidator();
broadcasterTokenValidator.RunValidator();

//
// Wait for signal to end application
//

try
{
    await applicationManagement.WaitForEndAsync();
}
catch (Exception ex)
{
    errorHandler.LogSystemException(ex);
}

//
// Stop webhost
//

await app.StopAsync();
