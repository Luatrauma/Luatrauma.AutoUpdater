using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Luatrauma.AutoUpdater
{
    static class Popup
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

        private const uint MB_OK = 0x0;
        private const uint MB_ICONINFORMATION = 0x40;
        private const uint MB_TOPMOST = 0x40000;

        public static void Show(string title, string message)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    MessageBoxW(IntPtr.Zero, message, title, MB_OK | MB_ICONINFORMATION | MB_TOPMOST);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    string escaped = message.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    string escapedTitle = title.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    RunAndWait("osascript", "-e", $"display dialog \"{escaped}\" with title \"{escapedTitle}\" buttons {{\"OK\"}} default button \"OK\"");
                }
                else if (OperatingSystem.IsLinux())
                {
                    if (!RunAndWait("zenity", "--info", "--no-wrap", $"--title={title}", $"--text={message}"))
                    {
                        RunAndWait("kdialog", "--title", title, "--msgbox", message);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log($"Failed to show popup: {e.Message}");
            }
        }

        private static bool RunAndWait(string fileName, params string[] arguments)
        {
            try
            {
                var info = new ProcessStartInfo(fileName) { UseShellExecute = false };
                foreach (string arg in arguments) { info.ArgumentList.Add(arg); }

                using var process = Process.Start(info);
                if (process == null) { return false; }

                process.WaitForExit();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
