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
    .AddTASDbContext<TASagentTwitchBot.Darkanine.Database.DatabaseContext>();
//Core Agnostic Systems
builder.Services
    .AddTASSingleton(TASagentTwitchBot.Core.Config.BotConfiguration.GetConfig(GetDefaultConfig()))
    .AddTASSingleton<TASagentTwitchBot.Core.CommunicationHandler>()
    .AddTASSingleton<TASagentTwitchBot.Core.View.BasicView>()
    .AddTASSingleton<TASagentTwitchBot.Core.ErrorHandler>()
    .AddTASSingleton<TASagentTwitchBot.Core.ApplicationManagement>()
    .AddTASSingleton<TASagentTwitchBot.Core.MessageAccumulator>();

//Custom Agnostic Systems
builder.Services
    .AddTASSingleton<TASagentTwitchBot.Darkanine.Configurator>();


//Core Twitch Systems
builder.Services
    .AddTASSingleton<TASagentTwitchBot.Core.API.Twitch.HelixHelper>()
    .AddTASSingleton<TASagentTwitchBot.Core.API.Twitch.BotTokenValidator>()
    .AddTASSingleton<TASagentTwitchBot.Core.API.Twitch.BroadcasterTokenValidator>()
    .AddTASSingleton<TASagentTwitchBot.Core.Database.UserHelper>();

//Core Twitch Chat Systems
builder.Services
    .AddTASSingleton<TASagentTwitchBot.Core.IRC.IrcClient>()
    .AddTASSingleton<TASagentTwitchBot.Core.IRC.IRCLogger>()
    .AddTASSingleton<TASagentTwitchBot.Core.Chat.ChatMessageHandler>();

//Custom Twitch Chat Systems
builder.Services
    .AddTASSingleton<TASagentTwitchBot.Darkanine.IRC.IRCNoticeIgnorer>();

//Core Scripting
builder.Services
    .AddTASSingleton<TASagentTwitchBot.Core.Scripting.ScriptManager>()
    .AddTASSingleton<TASagentTwitchBot.Core.Scripting.ScriptHelper>()
    .AddTASSingleton<TASagentTwitchBot.Core.Scripting.PersistentDataManager>();

builder.Services
    .AddTASSingleton(TASagentTwitchBot.Core.Commands.ScriptedCommands.ScriptedCommandsConfig.GetConfig())
    .AddTASSingleton<TASagentTwitchBot.Core.Commands.ScriptedCommands>();

//Core PubSub System
builder.Services
    .AddTASSingleton<TASagentTwitchBot.Core.PubSub.PubSubClient>()
    .AddTASSingleton<TASagentTwitchBot.Core.PubSub.RedemptionSystem>();

//Core Timer System
builder.Services.AddTASSingleton<TASagentTwitchBot.Core.Timer.TimerManager>();

//Command System
//Core Commands
builder.Services
    .AddTASSingleton<TASagentTwitchBot.Core.Commands.CommandSystem>()
    .AddTASSingleton<TASagentTwitchBot.Core.Commands.SystemCommandSystem>()
    .AddTASSingleton<TASagentTwitchBot.Darkanine.LimitedPermissionSystem>();

//Core Credit System
builder.Services
    .AddTASSingleton<TASagentTwitchBot.Core.Credit.DisabledCreditManager>();


//XInput Services
builder.Services
    .AddTASSingleton<TASagentTwitchBot.Plugin.XInput.ButtonPressDispatcher>();

//Custom Chat Listener
builder.Services
    .AddTASSingleton<TASagentTwitchBot.Darkanine.ChatListener>();

//Routing
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
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

//Config overrides
app.UseDocumentsOverrideContent();

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

foreach (TASagentTwitchBot.Core.IStartupListener startupListener in app.Services.GetServices<TASagentTwitchBot.Core.IStartupListener>())
{
    startupListener.NotifyStartup();
}

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


static TASagentTwitchBot.Core.Config.BotConfiguration GetDefaultConfig() =>
    new TASagentTwitchBot.Core.Config.BotConfiguration()
    {
        Version = TASagentTwitchBot.Core.Config.BotConfiguration.CURRENT_VERSION,
        AuthConfiguration = new TASagentTwitchBot.Core.Config.AuthConfiguration()
        {
            //Set admin password blank so it's prompted
            Admin = new TASagentTwitchBot.Core.Config.CredentialSet() { PasswordHash = "" },
            //Set non-admin passwords to nonsense, since they aren't required
            Privileged = new TASagentTwitchBot.Core.Config.CredentialSet() { PasswordHash = TASagentTwitchBot.Core.Cryptography.HashPassword(Guid.NewGuid().ToString()) },
            User = new TASagentTwitchBot.Core.Config.CredentialSet() { PasswordHash = TASagentTwitchBot.Core.Cryptography.HashPassword(Guid.NewGuid().ToString()) }
        }
    };