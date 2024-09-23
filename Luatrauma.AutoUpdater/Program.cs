using System.Diagnostics;

namespace Luatrauma.AutoUpdater
{
    internal class Program
    {
        public static string[] Args;

        static void Main(string[] args)
        {
            string tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "Luatrauma.AutoUpdater.Temp");
            Directory.CreateDirectory(tempFolder);

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Logger.Log("Unhandled exception: " + e.ExceptionObject);
            };

            Args = args;

            Task task = Start();
            task.Wait();
        }

        public async static Task Start()
        {
            List<string> args = new List<string>(Args);

            bool serverOnly = false;

            int index = args.FindIndex(x => x == "--server-only");
            if (index != -1)
            {
                args.RemoveAt(index);
                serverOnly = true;
            }

            await Updater.Update(serverOnly);

            if (args.Count > 0)
            {
                Logger.Log("Starting " + string.Join(" ", args));

                var info = new ProcessStartInfo
                {
                    FileName = args[0],
                    Arguments = string.Join(" ", args.Skip(1)),
                    WorkingDirectory = Path.GetDirectoryName(args[0]),
                };

                Process.Start(info);
            }
        }
    }
}
