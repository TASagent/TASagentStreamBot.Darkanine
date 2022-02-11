using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TASagentTwitchBot.Darkanine;

public class Configurator : Core.StandardConfigurator
{
    public Configurator(
        Core.Config.BotConfiguration botConfig,
        Core.ICommunication communication,
        Core.ErrorHandler errorHandler,
        Core.API.Twitch.HelixHelper helixHelper,
        Core.API.Twitch.IBotTokenValidator botTokenValidator,
        Core.API.Twitch.IBroadcasterTokenValidator broadcasterTokenValidator)
        : base(
            botConfig,
            communication,
            errorHandler,
            helixHelper,
            botTokenValidator,
            broadcasterTokenValidator)
    {

    }

    public override async Task<bool> VerifyConfigured()
    {
        bool successful = true;

        //Client Information
        successful |= ConfigureTwitchClient();

        //Check Accounts
        successful |= await ConfigureBotAccount(botTokenValidator);
        successful |= await ConfigureBroadcasterAccount(broadcasterTokenValidator, helixHelper);

        successful |= ConfigurePasswords();

        return successful;
    }
}
