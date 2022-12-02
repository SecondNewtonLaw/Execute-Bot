using Discord;
using Masked.DiscordNet;

namespace DiscordBot;

public static class CommandLoader
{
    public static CommandHelper LoadCommands(CommandHelper commandHelper)
    {
        commandHelper.AddToCommandList(new SlashCommandBuilder
        {
            Name = "ping",
            Description = "A simple command to show the latency between the server and the client (Discord Gateway) <---> (Bot)",
        }.Build(), Commands.Ping);

        commandHelper.AddToCommandList(Commands.BuildCommandRunner(), Commands.CommandRunner);

        return commandHelper;
    }
}