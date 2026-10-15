using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;

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
                var ex = e.ExceptionObject as Exception;
                string exString = ex?.ToString() ?? e.ExceptionObject.ToString()!;
                Logger.Log($"Unhandled exception: {exString}");
            };

            var rootCommand = new RootCommand("Luatrauma AutoUpdater");

            rootCommand.TreatUnmatchedTokensAsErrors = false;

            var optionServerOnly = new Option<bool>(name: "--server-only", description: "Downloads only the client patch.");
            optionServerOnly.SetDefaultValue(false);
            var optionNightly = new Option<bool>(name: "--nightly", description: "Downloads the nightly patch.");
            optionNightly.SetDefaultValue(false);
            var optionForceWindows = new Option<bool>(name: "--force-windows", description: "Downloads the patch for Windows OS regardless of the actual OS.");
            optionForceWindows.SetDefaultValue(false);

            /*
            var argumentRun = new Argument<string[]>("run", "The path to the Barotrauma executable that should be ran after the update finishes.")
            {
                Arity = ArgumentArity.ZeroOrMore
            };
            argumentRun.SetDefaultValue(null);
            */

            //rootCommand.AddArgument(argumentRun);
            rootCommand.AddOption(optionServerOnly);
            rootCommand.AddOption(optionNightly);
            rootCommand.AddOption(optionForceWindows);

            var varargs = new Argument<string[]>();
            rootCommand.AddArgument(varargs);

            rootCommand.SetHandler(async (InvocationContext ctx) =>
            {
                var nightly = ctx.ParseResult.GetValueForOption(optionNightly);
                var serverOnly = ctx.ParseResult.GetValueForOption(optionServerOnly);
                var forceWindows = ctx.ParseResult.GetValueForOption(optionForceWindows);

                await Updater.Update(nightly, serverOnly, forceWindows);

                // Steam linux forces me to do terrible things...
                string[] passthrough = ctx.ParseResult.GetValueForArgument(varargs);

                if (passthrough.Length > 0)
                {
                    string passthroughStringRepresentation = $"[{string.Join(", ", passthrough.Select(s => $"\"{s}\""))}]";
                    
                    Logger.Log("Starting " + passthroughStringRepresentation);

                    Process.Start(passthrough[0], passthrough.Skip(1));
                }
            });

            rootCommand.Invoke(args);
        }
    }
}
