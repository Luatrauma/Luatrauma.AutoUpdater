# Luatrauma.AutoUpdater

---

## Linux Installation Instructions

### 1. Download the Launcher Script

Place `launch-barotrauma.sh` directly into your main Barotrauma game directory (same as `Barotrauma.dll`).
For example: `~/.local/Steam/steamapps/common/Barotrauma/`.

### 2. Make the Script Executable

`cd ~/.local/.../Barotrauma/ && chmod +x launch-barotrauma.sh` OR `chmod +x ~/.local/.../Barotrauma/launch-barotrauma.sh`

### 3. Configure Steam Launch Options

Open Steam, right-click Barotrauma -> Properties -> General, and set your Launch Options to: `./launch-barotrauma.sh %command%`.

---

## How It Works on Linux

- **Isolated Structure:** The script automatically handles downloading `Luatrauma.AutoUpdater.linux-x64` into a dedicated subdirectory (`Luatrauma.AutoUpdater/`) to keep your game directory clean and prevent home-directory clutter.

- **Persistent Patching:** Once the patch is applied to Barotrauma.dll, it remains active across launches. The script checks for a new version of Luatrauma.AutoUpdater on each launch, with AutoUpdater checks for any framework updates automatically.

- **ETag Verification:** This script checks and compares the current ETag on each launch, only pulling the new image when there is a new version of Luatrama.AutoUpdater available.
