using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Luatrauma.AutoUpdater
{
    internal class Logger
    {
        public static void Log(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            string logEntry = $"[{DateTime.Now:O}] {message}";
            
            Console.ForegroundColor = color;
            Console.WriteLine(logEntry);
            Console.ForegroundColor = ConsoleColor.Gray;

            File.AppendAllText("Luatrauma.AutoUpdater.Temp/log.txt", logEntry + Environment.NewLine);
        }
    }
}
