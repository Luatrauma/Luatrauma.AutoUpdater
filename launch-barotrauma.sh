#!/usr/bin/env bash
set -euo pipefail

# Locate the absolute path of the directory, ensuring it works even when called from Steam
GAME_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &> /dev/null && pwd)"

UPDATER_DIR="$GAME_DIR/Luatrauma.AutoUpdater"
UPDATER_BIN="$UPDATER_DIR/Luatrauma.AutoUpdater.linux-x64"
TEMP_BIN="$UPDATER_DIR/Luatrauma.AutoUpdater.tmp"
HEADERS_TMP="$UPDATER_DIR/headers.tmp"
UPDATER_URL="https://github.com/Luatrauma/Luatrauma.AutoUpdater/releases/download/latest/Luatrauma.AutoUpdater.linux-x64"
ETAG_FILE="$UPDATER_DIR/etag.txt"

mkdir -p "$UPDATER_DIR"

# DEBUG: Redirect all script output to append to log file
#LOG_FILE="$UPDATER_DIR/wrapper.log"
#exec >> "$LOG_FILE" 2>&1
#echo "=== Luatrauma.AutoUpdater Wrapper Started: $(date) ==="

# Dynamic HTTP tool checker checks for the command or binary from inside Steam Runtime
HTTP_TOOL=""
if [ -x "/usr/bin/curl" ]; then
    HTTP_TOOL="/usr/bin/curl"
elif command -v curl &>/dev/null; then
    HTTP_TOOL="curl"
elif [ -x "/usr/bin/wget" ]; then
    HTTP_TOOL="/usr/bin/wget"
elif command -v wget &>/dev/null; then
    HTTP_TOOL="wget"
else
    echo "Warning: Neither curl nor wget was found. AutoUpdate check skipped." >&2
fi

# Clean the env prefix so steam doesn't break the curl/wget request / escape the runtime
CLEAN_ENV="env -u LD_LIBRARY_PATH -u LD_PRELOAD -u SSL_CERT_DIR -u SSL_CERT_FILE"


# Follow redirects to fetch headers from GitHub's servers to check the remote ETag or Last-Modified date
# Uses a timeout so that the script never hangs if nothing is found, defaults to local version.
fetch_etag() {
    if [ "$HTTP_TOOL" = "/usr/bin/curl" ] || [ "$HTTP_TOOL" = "curl" ]; then
        $CLEAN_ENV $HTTP_TOOL -vIL --max-time 5 "$UPDATER_URL" 2>&1 | grep -iE '^etag:' | tr -d '\r' | tail -n1 || true
    elif [ "$HTTP_TOOL" = "/usr/bin/wget" ] || [ "$HTTP_TOOL" = "wget" ]; then
        $CLEAN_ENV $HTTP_TOOL --debug --spider -S --timeout=5 "$UPDATER_URL" 2>&1 | grep -iE '^[[:space:]]*etag:' | tr -d '\r' | tail -n1 || true
    fi
}

# Download file and dump response headers
download_with_headers() {
    if [ "$HTTP_TOOL" = "/usr/bin/curl" ] || [ "$HTTP_TOOL" = "curl" ]; then
        $CLEAN_ENV curl -L -D "$HEADERS_TMP" -o "$TEMP_BIN" "$UPDATER_URL"
    elif [ "$HTTP_TOOL" = "/usr/bim/wget" ][ "$HTTP_TOOL" = "wget" ]; then
        $CLEAN_ENV wget -S -O "$TEMP_BIN" "$UPDATER_URL" 2> "$HEADERS_TMP"
    else
        return 1
    fi
}

# Check if there is a new binary version or if it's missing
need_update=false

if [ ! -f "$UPDATER_BIN" ]; then
    need_update=true
    echo "AutoUpdater binary not found. Downloading..."
elif [ -n "$HTTP_TOOL" ]; then
    echo "Checking for AutoUpdater updates..."
    remote_etag=$(fetch_etag)
        
        if [ -n "$remote_etag" ]; then
            local_etag=""
            [ -f "$ETAG_FILE" ] && local_etag=$(cat "$ETAG_FILE")

            if [ "$remote_etag" != "$local_etag" ]; then
                echo "A new version of Luatrauma AutoUpdater is available upstream."
                need_update=true
            else
                echo "AutoUpdater is already up to date, skipping download."
                need_update=false
            fi
    else
        echo "Warning: Could not reach update server. Using local version." >&2
    fi
fi

if [ "$need_update" = true ]; then
    echo "Downloading latest AutoUpdater..."

    # Download the latest binary and capture headers to save the new ETag
    # Download it to a temporary file first to ensure download is not corrupted
    # There should always be a known good file after init script exec.
    if download_with_headers; then
        chmod +x "$TEMP_BIN"
        mv "$TEMP_BIN" "$UPDATER_BIN"
    
        # Extract and save the new ETag if present
        if [ -f "$HEADERS_TMP" ]; then
            grep -iE '^etag:' "$UPDATER_DIR/headers.tmp" | tr -d '\r' | tail -n1 > "$ETAG_FILE" || true
        fi
    else
        echo "Error: Download failed." >&2
    fi
fi

# Clean up leftover temp files
rm -f "$TEMP_BIN" "$HEADERS_TMP"

# Execute the bin from the game directory, passing any arguements
if [ -f "$UPDATER_BIN" ]; then
    chmod +x "$UPDATER_BIN"
    cd "$GAME_DIR"
    "$UPDATER_BIN" "$@"
else
    echo "Error: Could not find or execute AutoUpdater binary." >&2
    exit 1
fi

