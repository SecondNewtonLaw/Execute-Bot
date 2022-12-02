using System.Diagnostics;
using System.Text;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Masked.DiscordNet.Extensions;
using Spectre.Console;

namespace DiscordBot;

#pragma warning disable MA0004 // Disable -> Use ConfigurateAwait(false); as no SyncCtx is needed.
public static partial class Commands
{
    private static readonly SemaphoreSlim semaphoreSlim = new(1);
    /// <summary>
    /// A command that allows a user to run commands in the Bot's Server.
    /// </summary>
    /// <param name="sockCommand">The interaction socket.</param>
    /// <returns>A Task representing the on-going asynchronous operation</returns>
    public static async Task CommandRunner(SocketSlashCommand sockCommand)
    {
        // Defer.
        await sockCommand.DeferAsync();
        bool execOnBash = (bool)sockCommand.Data.Options.ElementAt(0).Value;
        string? executableName = (string)sockCommand.Data.Options.ElementAt(1).Value;
        string? arguments = (string)sockCommand.Data.Options.ElementAt(2).Value;
        bool sudoRequired = (bool)sockCommand.Data.Options.ElementAt(3).Value;

        var botResponse = await sockCommand.FollowupAsync("Processing Request.");

        bool semaphoreAccessTimedOut = !await semaphoreSlim.WaitAsync(15000);

        if (semaphoreAccessTimedOut)
        {
            await botResponse.ModifyAsync(x => x.Content += "\nRequest Failure: Semaphore Lock Timed Out: Another Thread has the Semaphore Lock. Could not execute the process.");
        }
        executableName ??= "exit";

        if (sudoRequired && !sockCommand.User.GetGuildUser().GuildPermissions.Administrator)
            await botResponse.ModifyAsync(x => x.Content += "\nRequest Failure: Attempted to invoke a command which requires the usage of `sudo`, yet the user lacks permissions to do so.");

        string? processName;

        if (execOnBash)
            processName = "/bin/bash";
        else
            processName = executableName;

        if (arguments.ToLower() == "null")
            arguments = "";
        
        ProcessStartInfo processInformation = new()
        {
            FileName = sudoRequired ? "sudo " + processName : processName,
            Arguments = arguments,
            RedirectStandardInput = true,
        };
        Process? processInstance = null;
        await botResponse.ModifyAsync(x => x.Content += "\nAttempting to Start the Requested Process.");
        try
        {
            processInstance = Process.Start(processInformation);
            await botResponse.ModifyAsync(x => x.Content += "\nProcess Started. Using password if `sudo` was required... | Process Watcher moved to a Separate Thread.");

            Thread watcher = new(async () =>
            {
                try
                {
                    if (sudoRequired)
                    {
                        // Wait a Delay before attempting to write the `sudo` password.
                        await Task.Delay(500);
                        await processInstance?.StandardInput.WriteLineAsync(await File.ReadAllTextAsync("PASSWORD", Encoding.UTF8))!;
                        await Task.Delay(500);
                    }
                    await processInstance?.WaitForExitAsync()!;
                    await botResponse.ModifyAsync(x => x.Content += "\nProcess Exited. Attaching Complete Output.");

                    Stream stdOut = processInstance.StandardOutput.BaseStream;

                    await botResponse.ModifyAsync(x =>
                    {
                        x.Content += "\nProcess Exited. Output Attached";
                        List<FileAttachment> attachments = (List<FileAttachment>)Enumerable.Empty<FileAttachment>();
                        attachments.Add(new FileAttachment(stdOut, "stdout.txt", "The standard output stream of the Executable.", true));
                        x.Attachments = attachments;
                    });
                }
                finally
                {
                    if (!(processInstance?.HasExited)!.Value)
                        processInstance?.Kill();        // Kill the process, if not already dead. 
                    processInstance?.Dispose();
                    semaphoreSlim.Release();            // Avoid a DeadLock by releasing the Semaphore.
                }
            });
            watcher.Start();
            return;
        }
        catch (Exception ex)
        {
            await botResponse.ModifyAsync(x => x.Content = $"Process Initialization failure: Failed at {DateTime.UtcNow} with error `{ex}`");
            processInstance?.Dispose();
            semaphoreSlim.Release();                    // Avoid a DeadLock by releasing the Semaphore.
            return;
        }
    }
    /// <summary>
    /// A command that builds the MetaData for the CommandRunner command.
    /// </summary>
    /// <returns>The commands' MetaData.</returns>
    public static SlashCommandProperties BuildCommandRunner()
    {
        var cmdBuild = new SlashCommandBuilder
        {
            Name = "exec",
            Description = "Executes a command remotely on the discord bot server.",
        };
        cmdBuild.AddOption("usebash", ApplicationCommandOptionType.Boolean, "States wether or not the bot should run this command using the Bash script interpreter", true, false);
        cmdBuild.AddOption("executable", ApplicationCommandOptionType.String, "The path to the executable used to perform the operation", true, false);
        cmdBuild.AddOption("arguments", ApplicationCommandOptionType.String, "The arguments given to the executable to do it's job", true, false);
        cmdBuild.AddOption("sudorequired", ApplicationCommandOptionType.Boolean, "Does the command require sudo?", true, false);

        return cmdBuild.Build();
    }
}
