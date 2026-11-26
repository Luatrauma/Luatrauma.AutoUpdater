using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Luatrauma.AutoUpdater
{
    static class Updater
    {
        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }

        private static async Task<string?> GetRemoteETag(string url)
        {
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await client.SendAsync(request);

            return response.Headers.ETag?.Tag;
        }

        private static readonly Version LastSupportedGameVersion = new Version(1, 13, 5, 0);

        private const string ObsoleteMessage = "LuaCsForBarotrauma is now installed through the Steam Workshop, so you can remove it from your Steam launch options. " +
            "To keep using it, subscribe to the mod on the Workshop and enable it in your mod list.";

        private static bool IsLaunchParameterMethodObsolete(bool serverOnly)
        {
            string dllFile = serverOnly ? "DedicatedServer.dll" : "Barotrauma.dll";
            string currentDll = Path.Combine(Directory.GetCurrentDirectory(), dllFile);

            if (!File.Exists(currentDll)) { return false; }

            var info = FileVersionInfo.GetVersionInfo(currentDll);
            var gameVersion = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);

            if (gameVersion <= LastSupportedGameVersion) { return false; }

            Logger.Log($"Game version {gameVersion} is above {LastSupportedGameVersion}, skipping patch.", ConsoleColor.Yellow);
            Logger.Log(ObsoleteMessage, ConsoleColor.Yellow);


            string localModFolder = Path.Combine(Directory.GetCurrentDirectory(), "LocalMods", "LuaCsForBarotrauma");

            if (Directory.Exists(localModFolder))
            {
                try
                {
                    Directory.Delete(localModFolder, true);
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to remove previous install at {localModFolder}: {e.Message}", ConsoleColor.Red);
                }
            }

            if (!serverOnly)
            {
                Popup.Show("LuaCsForBarotrauma", ObsoleteMessage);
            }

            return true;
        }

        public async static Task Update(bool nightly = false, bool serverOnly = false)
        {
            Logger.Log("Starting update...");

            if (IsLaunchParameterMethodObsolete(serverOnly))
            {
                return;
            }

            string patchUrl = "https://github.com/evilfactory/LuaCsForBarotrauma/releases/download/latest/";
            if (nightly)
            {
                patchUrl = "https://github.com/evilfactory/LuaCsForBarotrauma/releases/download/nightly/";
            }
            if (OperatingSystem.IsWindows())
            {
                if (serverOnly) { patchUrl += "luacsforbarotrauma_patch_windows_server.zip"; }
                else { patchUrl += "luacsforbarotrauma_patch_windows_client.zip"; }
            }
            else if (OperatingSystem.IsLinux())
            {
                if (serverOnly) { patchUrl += "luacsforbarotrauma_patch_linux_server.zip"; }
                else { patchUrl += "luacsforbarotrauma_patch_linux_client.zip"; }
            }
            else if (OperatingSystem.IsMacOS())
            {
                if (serverOnly) { patchUrl += "luacsforbarotrauma_patch_mac_server.zip"; }
                else { patchUrl += "luacsforbarotrauma_patch_mac_client.zip"; }
            }

            if (patchUrl == null)
            {
                Logger.Log("Unsupported operating system.");
                return;
            }

            string tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "Luatrauma.AutoUpdater.Temp");
            string patchZip = Path.Combine(tempFolder, "patch.zip");
            string extractionFolder = Path.Combine(tempFolder, "Extracted");

            string etagFile = Path.Combine(tempFolder, "patch.etag");

            string? remoteEtag = await GetRemoteETag(patchUrl);
            string? localEtag = File.Exists(etagFile) ? await File.ReadAllTextAsync(etagFile) : null;

            if (false && remoteEtag != null && remoteEtag == localEtag)
            {
                Logger.Log("Patch has not changed. Skipping download.");
            }
            else
            {
                if (remoteEtag != null)
                {
                    await File.WriteAllTextAsync(etagFile, remoteEtag);
                }

                Logger.Log($"Downloading patch zip from {patchUrl}");

                try
                {
                    using var client = new HttpClient();

                    byte[] fileBytes = await client.GetByteArrayAsync(patchUrl);

                    await File.WriteAllBytesAsync(patchZip, fileBytes);
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to download patch zip: {e.Message}");
                    return;
                }

                Logger.Log($"Downloaded patch zip to {patchZip}");
            }

            try
            {
                if (Directory.Exists(extractionFolder))
                {
                    Directory.Delete(extractionFolder, true);
                }
                Directory.CreateDirectory(extractionFolder);

                ZipFile.ExtractToDirectory(patchZip, extractionFolder, true);

            }
            catch (Exception e)
            {
                Logger.Log($"Failed to extract patch zip: {e.Message}");
                return;
            }

            Logger.Log($"Extracted patch zip to {Directory.GetCurrentDirectory()}");

            Logger.Log($"Applying patch...");

            string dllFile = "Barotrauma.dll";
            if (serverOnly)
            {
                dllFile = "DedicatedServer.dll";
            }

            // Verify that the dll version is the same as the current one
            string currentDll = Path.Combine(Directory.GetCurrentDirectory(), dllFile);
            string newDll = Path.Combine(extractionFolder, dllFile);

            if (!File.Exists(currentDll))
            {
                Logger.Log($"Failed to find the current {dllFile}", ConsoleColor.Red);
                return;
            }

            if (!File.Exists(newDll))
            {
                Logger.Log($"Failed to find the new {dllFile}", ConsoleColor.Red);
                return;
            }

            // Grab the version of the current dll
            var currentVersion = FileVersionInfo.GetVersionInfo(currentDll);
            var newVersion = FileVersionInfo.GetVersionInfo(newDll);

            if (currentVersion == null || newVersion == null)
            {
                Logger.Log("Failed to get version info for the dlls");
                return;
            }

            if (currentVersion.FileVersion == newVersion.FileVersion)
            {
                Logger.Log($"The patch is compatible with the current game version {newVersion.FileVersion}.");
            }
            else
            {
                Logger.Log($"The patch is not compatible with the current game version {currentVersion.FileVersion} -> {newVersion.FileVersion}, aborting.");

                Logger.Log("Theres no patch available for the current game version, the game will be launched without the patch, if there was a new game update please wait until a new patch is released.", ConsoleColor.Yellow);

                await Task.Delay(8000);

                return;
            }

            CopyFilesRecursively(extractionFolder, Directory.GetCurrentDirectory());

            Logger.Log("Patch applied.");

            if (File.Exists("luacsversion.txt")) // Workshop stuff, get rid of it so it doesn't interfere
            {
                File.Delete("luacsversion.txt");
            }

            Logger.Log("Update completed.");
        }
    }
}
