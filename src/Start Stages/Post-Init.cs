using System;
using System.Linq;
using Discord;
using Masked.Sys.Extensions;
using Spectre.Console;

namespace DiscordBot;

public partial class StartStage
{
    public async Task PostInitialization()
    {
        AnsiConsole.MarkupLine("[maroon][[INFO]] Running [yellow]Post-Initialization[/].[/]");
    }
}