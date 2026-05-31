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

        public async static Task Update(bool nightly = false, bool serverOnly = false, bool forceWindows = false)
        {
            Logger.Log("Starting update...");

            string patchUrl = "https://github.com/evilfactory/LuaCsForBarotrauma/releases/download/latest/";
            if (nightly)
            {
                patchUrl = "https://github.com/evilfactory/LuaCsForBarotrauma/releases/download/nightly/";
            }
            if (OperatingSystem.IsWindows() || forceWindows)
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
            else
            {
                Logger.Log("Unsupported operating system.");
                return;
            }
            
            Logger.Log($"{nameof(patchUrl)} = {patchUrl}");

            string tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "Luatrauma.AutoUpdater.Temp");
            string patchZip = Path.Combine(tempFolder, "patch.zip");
            string extractionFolder = Path.Combine(tempFolder, "Extracted");

            string etagFile = Path.Combine(tempFolder, "patch.etag");

            string? remoteEtag = await GetRemoteETag(patchUrl);
            string? localEtag = File.Exists(etagFile) ? await File.ReadAllTextAsync(etagFile) : null;
            
            Logger.Log($"{nameof(remoteEtag)} = {remoteEtag}");
            Logger.Log($"{nameof(localEtag)}  = {localEtag}");

            bool skippedDownload = false;
            if (remoteEtag is not null && remoteEtag == localEtag)
            {
                skippedDownload = true;
                Logger.Log("Patch has not changed. Skipping download.");
            }
            else
            {
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
                
                if (remoteEtag is not null)
                {
                    await File.WriteAllTextAsync(etagFile, remoteEtag);
                }
            }

            string dllFile = "Barotrauma.dll";
            if (serverOnly)
            {
                dllFile = "DedicatedServer.dll";
            }
            
            Logger.Log($"Applying patch...");

            string extractEtagFilePath = Path.Combine(tempFolder, "extract.etag");
            try
            {
                if (!skippedDownload)
                {
                    throw new Exception();
                }
                if (!Directory.Exists(extractionFolder))
                {
                    throw new Exception();
                }

                string? extractEtag = File.Exists(extractEtagFilePath) ? await File.ReadAllTextAsync(extractEtagFilePath) : null;

                Logger.Log($"{nameof(remoteEtag)}  = {remoteEtag}");
                Logger.Log($"{nameof(extractEtag)} = {extractEtag}");

                if (extractEtag is null || extractEtag != remoteEtag)
                {
                    throw new Exception();
                }

                Logger.Log("Files inside the patch zip is already extracted to the extraction folder. New extraction skipped.");
            }
            catch (Exception)
            {
                Logger.Log("Existing files inside the extraction folder are outdated or simply non-existing. Performing new extraction...");
                
                if (Directory.Exists(extractionFolder))
                {
                    Directory.Delete(extractionFolder, true);
                }

                Directory.CreateDirectory(extractionFolder);

                try
                {
                    ZipFile.ExtractToDirectory(patchZip, extractionFolder, true);
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to extract patch zip: {e.Message}");
                    return;
                }

                if (remoteEtag is not null)
                {
                    await File.WriteAllTextAsync(extractEtagFilePath, remoteEtag);
                }
                else
                {
                    File.Delete(extractEtagFilePath);
                }

                Logger.Log($"Extracted patch zip to {extractionFolder}");
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
            
            Logger.Log($"current ddl version: {currentVersion.FileVersion}");
            Logger.Log($"new ddl version:     {newVersion.FileVersion}");

            if (currentVersion.FileVersion is null || newVersion.FileVersion is null)
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

            string applyEtagFilePath = Path.Combine(tempFolder, "apply.etag");
            string? applyEtag = File.Exists(applyEtagFilePath) ? await File.ReadAllTextAsync(applyEtagFilePath) : null;
            
            Logger.Log($"{nameof(remoteEtag)} = {remoteEtag}");
            Logger.Log($"{nameof(applyEtag)}  = {applyEtag}");
            
            if (applyEtag is not null && applyEtag == remoteEtag)
            {
                Logger.Log("Game is already modded with the latest patch. Patch skipped.");
            }
            else
            {
                CopyFilesRecursively(extractionFolder, Directory.GetCurrentDirectory());

                Logger.Log("Patch applied.");

                if (remoteEtag is not null)
                {
                    await File.WriteAllTextAsync(applyEtagFilePath, remoteEtag);
                }
                else
                {
                    File.Delete(applyEtagFilePath);
                }
            }

            if (File.Exists("luacsversion.txt")) // Workshop stuff, get rid of it so it doesn't interfere
            {
                File.Delete("luacsversion.txt");
            }

            Logger.Log("Update completed.");
        }
    }
}
