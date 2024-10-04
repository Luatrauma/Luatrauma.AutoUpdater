using System.Diagnostics;
using System.CommandLine;

namespace Luatrauma.AutoUpdater
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "Luatrauma.AutoUpdater.Temp");
            Directory.CreateDirectory(tempFolder);

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Logger.Log("Unhandled exception: " + e.ExceptionObject);
            };

            var rootCommand = new RootCommand("Luatrauma AutoUpdater");

            var optionServerOnly = new Option<bool>(name: "--server-only", description: "Downloads only the client patch.");
            optionServerOnly.SetDefaultValue(false);
            var optionNightly = new Option<bool>(name: "--nightly", description: "Downloads the nightly patch.");
            optionNightly.SetDefaultValue(false);
            var argumentRun = new Argument<string?>("run", "The path to the Barotrauma executable that should be ran after the update finishes.");
            argumentRun.SetDefaultValue(null);

            rootCommand.AddArgument(argumentRun);
            rootCommand.AddOption(optionServerOnly);
            rootCommand.AddOption(optionNightly);

            rootCommand.SetHandler(async (string? runExe, bool serverOnly, bool nightly) =>
            {
                await Updater.Update(serverOnly);

                if (runExe != null)
                {
                    Logger.Log("Starting " + string.Join(" ", runExe));

                    var info = new ProcessStartInfo
                    {
                        FileName = runExe,
                        WorkingDirectory = Path.GetDirectoryName(runExe)
                    };

                    Process.Start(info);
                }
            }, argumentRun, optionServerOnly, optionNightly);

            rootCommand.Invoke(args);
        }
    }
}
