#!/usr/bin/env bash
set -euo pipefail

# STS2 CLI Mod Installer for WSL / macOS / Linux
# Downloads and installs the CLI tool and game mod from GitHub Releases.
#
# Install (latest):
#   curl -fsSL https://raw.githubusercontent.com/longkerdandy/STS2-Cli-Mod/main/install.sh | bash
#
# Install specific version:
#   curl -fsSL https://raw.githubusercontent.com/longkerdandy/STS2-Cli-Mod/main/install.sh | bash -s -- -v 0.102.1
#
# Uninstall:
#   curl -fsSL https://raw.githubusercontent.com/longkerdandy/STS2-Cli-Mod/main/install.sh | bash -s -- --uninstall

# ── Colors ─────────────────────────────────────────────────────────────────────

CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
RED='\033[0;31m'
MUTED='\033[0;2m'
BOLD='\033[1m'
NC='\033[0m'

# ── Constants ──────────────────────────────────────────────────────────────────

REPO="longkerdandy/STS2-Cli-Mod"
GAME_NAME="Slay the Spire 2"
MOD_FILES=("STS2.Cli.Mod.dll" "STS2.Cli.Mod.json")

# ── Arguments ──────────────────────────────────────────────────────────────────

requested_version=""
game_dir=""
cli_dir=""
skip_mod=false
skip_cli=false
no_modify_path=false
uninstall=false

usage() {
    cat <<EOF
STS2 CLI Mod Installer

Usage: install.sh [options]

Options:
    -h, --help              Display this help message
    -v, --version <version> Install a specific version (e.g., 0.102.1)
    -g, --game-dir <path>   Specify the game installation directory
    -c, --cli-dir <path>    Specify the CLI installation directory
        --skip-mod          Skip mod installation
        --skip-cli          Skip CLI installation
        --no-modify-path    Don't modify shell config files
        --uninstall         Uninstall STS2 CLI Mod

Examples:
    curl -fsSL https://raw.githubusercontent.com/$REPO/main/install.sh | bash
    curl -fsSL https://raw.githubusercontent.com/$REPO/main/install.sh | bash -s -- -v 0.102.1
    ./install.sh --game-dir "/path/to/Slay the Spire 2"
    ./install.sh --uninstall
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -h|--help)
            usage
            exit 0
            ;;
        -v|--version)
            if [[ -n "${2:-}" ]]; then
                requested_version="${2#v}"
                shift 2
            else
                echo -e "${RED}  Error: --version requires a version argument${NC}"
                exit 1
            fi
            ;;
        -g|--game-dir)
            if [[ -n "${2:-}" ]]; then
                game_dir="$2"
                shift 2
            else
                echo -e "${RED}  Error: --game-dir requires a path argument${NC}"
                exit 1
            fi
            ;;
        -c|--cli-dir)
            if [[ -n "${2:-}" ]]; then
                cli_dir="$2"
                shift 2
            else
                echo -e "${RED}  Error: --cli-dir requires a path argument${NC}"
                exit 1
            fi
            ;;
        --skip-mod) skip_mod=true; shift ;;
        --skip-cli) skip_cli=true; shift ;;
        --no-modify-path) no_modify_path=true; shift ;;
        --uninstall) uninstall=true; shift ;;
        *)
            echo -e "${YELLOW}  Warning: Unknown option '$1'${NC}" >&2
            shift
            ;;
    esac
done

# ── Helpers ────────────────────────────────────────────────────────────────────

step()    { echo -e "${CYAN}  $1${NC}"; }
success() { echo -e "${GREEN}  $1${NC}"; }
warn()    { echo -e "${YELLOW}  $1${NC}"; }
err()     { echo -e "${RED}  $1${NC}"; }
muted()   { echo -e "${MUTED}  $1${NC}"; }

# ── Platform Detection ─────────────────────────────────────────────────────────

detect_platform() {
    local raw_os
    raw_os=$(uname -s)
    is_wsl=false

    case "$raw_os" in
        Linux*)
            os="linux"
            # Detect WSL
            if [[ -f /proc/version ]] && grep -qi microsoft /proc/version 2>/dev/null; then
                is_wsl=true
            elif [[ -d /mnt/c/ ]]; then
                is_wsl=true
            fi
            ;;
        Darwin*) os="darwin" ;;
        MINGW*|MSYS*|CYGWIN*) os="windows" ;;
        *)
            err "Unsupported operating system: $raw_os"
            exit 1
            ;;
    esac

    arch=$(uname -m)
    case "$arch" in
        x86_64)  arch="x64" ;;
        aarch64) arch="arm64" ;;
        arm64)   arch="arm64" ;;
        *)
            err "Unsupported architecture: $arch"
            exit 1
            ;;
    esac

    # Rosetta detection on macOS
    if [[ "$os" == "darwin" && "$arch" == "x64" ]]; then
        local rosetta
        rosetta=$(sysctl -n sysctl.proc_translated 2>/dev/null || echo 0)
        if [[ "$rosetta" == "1" ]]; then
            arch="arm64"
        fi
    fi
}

# ── CLI Install Directory Resolution ──────────────────────────────────────────

resolve_cli_dir() {
    # If explicitly set, use it
    if [[ -n "$cli_dir" ]]; then
        echo "$cli_dir"
        return
    fi

    # Environment variable override
    if [[ -n "${STS2_CLI_DIR:-}" ]]; then
        echo "$STS2_CLI_DIR"
        return
    fi

    if [[ "$is_wsl" == "true" ]]; then
        # WSL: install to Windows side %LOCALAPPDATA%\sts2-cli
        local win_appdata
        win_appdata=$(cmd.exe /c "echo %LOCALAPPDATA%" 2>/dev/null | tr -d '\r') || true
        if [[ -n "$win_appdata" ]]; then
            local wsl_appdata
            wsl_appdata=$(wslpath -u "$win_appdata" 2>/dev/null) || true
            if [[ -n "$wsl_appdata" ]]; then
                echo "$wsl_appdata/sts2-cli"
                return
            fi
        fi
        # Fallback: try common WSL path
        local user_dir
        user_dir=$(ls -d /mnt/c/Users/*/ 2>/dev/null | grep -v -i -E '(Public|Default|All Users)' | head -1) || true
        if [[ -n "$user_dir" ]]; then
            echo "${user_dir}AppData/Local/sts2-cli"
            return
        fi
    fi

    # macOS / native Linux
    echo "$HOME/.local/bin"
}

# ── Steam Game Directory Detection ────────────────────────────────────────────

find_steam_libraries() {
    # Returns all Steam library base paths (one per line)
    local vdf_path=""
    local default_steam=""

    if [[ "$is_wsl" == "true" || "$os" == "windows" ]]; then
        # Windows / WSL: Steam default install
        default_steam="/mnt/c/Program Files (x86)/Steam"
        vdf_path="$default_steam/config/libraryfolders.vdf"
    elif [[ "$os" == "darwin" ]]; then
        default_steam="$HOME/Library/Application Support/Steam"
        vdf_path="$default_steam/config/libraryfolders.vdf"
    else
        default_steam="$HOME/.local/share/Steam"
        vdf_path="$default_steam/config/libraryfolders.vdf"
    fi

    # Output default path if it exists
    if [[ -d "$default_steam/steamapps/common" ]]; then
        echo "$default_steam"
    fi

    # Parse libraryfolders.vdf for additional paths
    if [[ -f "$vdf_path" ]]; then
        # Extract "path" values, handle both \\  and / separators
        grep -oP '"path"\s+"\K[^"]+' "$vdf_path" 2>/dev/null | while IFS= read -r lib_path; do
            # Convert double backslashes to single (Windows paths in VDF)
            lib_path="${lib_path//\\\\/\\}"
            # On WSL, convert Windows paths to WSL paths
            if [[ "$is_wsl" == "true" && "$lib_path" == *":"* ]]; then
                lib_path=$(wslpath -u "$lib_path" 2>/dev/null) || continue
            fi
            # Skip if same as default or doesn't exist
            if [[ "$lib_path" != "$default_steam" && -d "$lib_path" ]]; then
                echo "$lib_path"
            fi
        done
    fi
}

find_game_directory() {
    # Priority: 1. --game-dir  2. STS2_GAME_DIR  3. Steam scan

    # 1. Explicit parameter
    if [[ -n "$game_dir" ]]; then
        if [[ -d "$game_dir" ]]; then
            echo "$game_dir"
            return
        fi
        err "Specified game directory not found: $game_dir"
        return
    fi

    # 2. Environment variable
    if [[ -n "${STS2_GAME_DIR:-}" && -d "$STS2_GAME_DIR" ]]; then
        echo "$STS2_GAME_DIR"
        return
    fi

    # 3. Scan all Steam libraries
    while IFS= read -r lib; do
        local candidate="$lib/steamapps/common/$GAME_NAME"
        if [[ -d "$candidate" ]]; then
            echo "$candidate"
            return
        fi
    done < <(find_steam_libraries)
}

# ── Runtime ID ─────────────────────────────────────────────────────────────────

resolve_rid() {
    # For WSL, we need the Windows binary
    if [[ "$is_wsl" == "true" ]]; then
        echo "win-x64"
        return
    fi

    case "$os-$arch" in
        linux-x64)      echo "linux-x64" ;;
        linux-arm64)    echo "linux-arm64" ;;
        darwin-x64)     echo "osx-x64" ;;
        darwin-arm64)   echo "osx-arm64" ;;
        windows-x64)    echo "win-x64" ;;
        *)
            err "Unsupported platform: $os-$arch"
            exit 1
            ;;
    esac
}

resolve_exe_name() {
    local rid="$1"
    if [[ "$rid" == "win-x64" ]]; then
        echo "sts2.exe"
    else
        echo "sts2"
    fi
}

resolve_archive_ext() {
    local rid="$1"
    if [[ "$rid" == "win-x64" ]]; then
        echo "zip"
    else
        echo "tar.gz"
    fi
}

# ── Version Helpers ────────────────────────────────────────────────────────────

get_latest_release_tag() {
    # Primary: GitHub REST API
    local tag
    tag=$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" \
        -H "Accept: application/vnd.github+json" \
        -H "User-Agent: STS2-Installer" 2>/dev/null \
        | sed -n 's/.*"tag_name": *"\([^"]*\)".*/\1/p') || true

    if [[ -n "$tag" ]]; then
        echo "$tag"
        return 0
    fi

    # Fallback: parse redirect Location from releases/latest page (no API quota)
    muted "  GitHub API unavailable (rate limit?), trying fallback..."
    local location
    location=$(curl -sI "https://github.com/$REPO/releases/latest" \
        -H "User-Agent: STS2-Installer" 2>/dev/null \
        | sed -n 's/^[Ll]ocation: *.*\/tag\/\(.*\)/\1/p' | tr -d '\r\n') || true

    if [[ -n "$location" ]]; then
        echo "$location"
        return 0
    fi

    err "Failed to fetch latest release tag from GitHub"
    return 1
}

get_installed_cli_version() {
    local exe_path="$1"
    if [[ -f "$exe_path" && -x "$exe_path" ]]; then
        local ver
        ver=$("$exe_path" --version 2>/dev/null) || true
        if [[ -n "$ver" ]]; then
            echo "$ver"
        fi
    fi
}

# ── PATH Management (bash) ────────────────────────────────────────────────────

add_to_shell_path() {
    local dir="$1"

    # On WSL with Windows binary, skip PATH modification (user should use full path or alias)
    if [[ "$is_wsl" == "true" ]]; then
        return 1  # signal "not added", caller handles alias setup
    fi

    # Check if already in PATH
    if echo ":$PATH:" | grep -q ":$dir:"; then
        return 1  # already present
    fi

    local current_shell
    current_shell=$(basename "${SHELL:-/bin/bash}")
    local config_files=""

    case "$current_shell" in
        fish)  config_files="$HOME/.config/fish/config.fish" ;;
        zsh)   config_files="${ZDOTDIR:-$HOME}/.zshrc" ;;
        bash)  config_files="$HOME/.bashrc $HOME/.bash_profile" ;;
        *)     config_files="$HOME/.bashrc $HOME/.profile" ;;
    esac

    local config_file=""
    for f in $config_files; do
        if [[ -f "$f" ]]; then
            config_file="$f"
            break
        fi
    done

    if [[ -z "$config_file" ]]; then
        warn "No shell config found. Manually add to PATH:"
        muted "  export PATH=$dir:\$PATH"
        return 1
    fi

    local command=""
    if [[ "$current_shell" == "fish" ]]; then
        command="fish_add_path $dir"
    else
        command="export PATH=$dir:\$PATH"
    fi

    if grep -Fxq "$command" "$config_file" 2>/dev/null; then
        return 1  # already present in config
    fi

    echo "" >> "$config_file"
    echo "# sts2-cli" >> "$config_file"
    echo "$command" >> "$config_file"
    return 0
}

setup_wsl_alias() {
    local exe_path="$1"

    local current_shell
    current_shell=$(basename "${SHELL:-/bin/bash}")
    local config_file=""

    case "$current_shell" in
        fish)  config_file="$HOME/.config/fish/config.fish" ;;
        zsh)   config_file="${ZDOTDIR:-$HOME}/.zshrc" ;;
        bash)
            for f in "$HOME/.bashrc" "$HOME/.bash_profile"; do
                if [[ -f "$f" ]]; then config_file="$f"; break; fi
            done
            ;;
        *)
            for f in "$HOME/.bashrc" "$HOME/.profile"; do
                if [[ -f "$f" ]]; then config_file="$f"; break; fi
            done
            ;;
    esac

    if [[ -z "$config_file" ]]; then
        warn "No shell config found. Manually add alias:"
        muted "  alias sts2='$exe_path'"
        return
    fi

    local alias_cmd=""
    if [[ "$current_shell" == "fish" ]]; then
        alias_cmd="alias sts2 '$exe_path'"
    else
        alias_cmd="alias sts2='$exe_path'"
    fi

    if grep -Fq "alias sts2=" "$config_file" 2>/dev/null || \
       grep -Fq "alias sts2 " "$config_file" 2>/dev/null; then
        # Update existing alias
        if [[ "$current_shell" == "fish" ]]; then
            sed -i "s|alias sts2 .*|$alias_cmd|" "$config_file"
        else
            sed -i "s|alias sts2=.*|$alias_cmd|" "$config_file"
        fi
        success "Updated sts2 alias in $config_file"
    else
        echo "" >> "$config_file"
        echo "# sts2-cli" >> "$config_file"
        echo "$alias_cmd" >> "$config_file"
        success "Added sts2 alias to $config_file"
    fi
}

remove_shell_path_and_alias() {
    local dir="$1"

    local config_files="$HOME/.bashrc $HOME/.bash_profile $HOME/.profile ${ZDOTDIR:-$HOME}/.zshrc $HOME/.config/fish/config.fish"
    for f in $config_files; do
        if [[ -f "$f" ]]; then
            # Remove PATH export lines
            sed -i "\|export PATH=$dir|d" "$f" 2>/dev/null || true
            sed -i "\|fish_add_path $dir|d" "$f" 2>/dev/null || true
            # Remove alias lines
            sed -i '/alias sts2=/d' "$f" 2>/dev/null || true
            sed -i "/alias sts2 '/d" "$f" 2>/dev/null || true
            # Remove comment lines left behind
            sed -i '/^# sts2-cli$/d' "$f" 2>/dev/null || true
            # Clean up empty trailing lines
            sed -i -e :a -e '/^\n*$/{$d;N;ba' -e '}' "$f" 2>/dev/null || true
        fi
    done
}

# ── Zip Extraction ─────────────────────────────────────────────────────────────

extract_zip() {
    # Extract a zip file to a directory, using unzip or python3 as fallback
    local zip_file="$1"
    local dest_dir="$2"

    if command -v unzip >/dev/null 2>&1; then
        unzip -q "$zip_file" -d "$dest_dir"
    elif command -v python3 >/dev/null 2>&1; then
        python3 -c "
import zipfile, sys
with zipfile.ZipFile(sys.argv[1], 'r') as z:
    z.extractall(sys.argv[2])
" "$zip_file" "$dest_dir"
    else
        err "Neither 'unzip' nor 'python3' is available. Please install one of them."
        return 1
    fi
}

# ── Download & Install ─────────────────────────────────────────────────────────

install_cli() {
    local tag="$1"
    local install_dir="$2"
    local ver="${tag#v}"
    local rid
    rid=$(resolve_rid)
    local exe_name
    exe_name=$(resolve_exe_name "$rid")
    local archive_ext
    archive_ext=$(resolve_archive_ext "$rid")

    local file_name="sts2-cli-v${ver}-${rid}.${archive_ext}"
    local url="https://github.com/$REPO/releases/download/$tag/$file_name"

    local tmp_dir
    tmp_dir=$(mktemp -d)

    step "Downloading CLI ($file_name)..."
    if ! curl -fSL -o "$tmp_dir/$file_name" "$url"; then
        err "Failed to download CLI"
        muted "URL: $url"
        rm -rf "$tmp_dir"
        return 1
    fi

    step "Extracting..."
    if [[ "$archive_ext" == "zip" ]]; then
        if ! extract_zip "$tmp_dir/$file_name" "$tmp_dir"; then
            rm -rf "$tmp_dir"
            return 1
        fi
    else
        tar -xzf "$tmp_dir/$file_name" -C "$tmp_dir"
    fi

    # Find the executable
    local exe_src="$tmp_dir/$exe_name"
    if [[ ! -f "$exe_src" ]]; then
        exe_src=$(find "$tmp_dir" -name "$exe_name" -type f | head -1)
    fi
    if [[ -z "$exe_src" || ! -f "$exe_src" ]]; then
        err "Could not find $exe_name in the downloaded archive"
        rm -rf "$tmp_dir"
        return 1
    fi

    mkdir -p "$install_dir"
    cp "$exe_src" "$install_dir/$exe_name"
    chmod 755 "$install_dir/$exe_name"
    rm -rf "$tmp_dir"

    success "CLI installed to $install_dir/$exe_name"
    return 0
}

install_mod() {
    local tag="$1"
    local mod_dir="$2"
    local ver="${tag#v}"

    local file_name="sts2-mod-v${ver}.zip"
    local url="https://github.com/$REPO/releases/download/$tag/$file_name"

    local tmp_dir
    tmp_dir=$(mktemp -d)

    step "Downloading Mod ($file_name)..."
    if ! curl -fSL -o "$tmp_dir/$file_name" "$url"; then
        err "Failed to download Mod"
        muted "URL: $url"
        rm -rf "$tmp_dir"
        return 1
    fi

    step "Extracting..."
    if ! extract_zip "$tmp_dir/$file_name" "$tmp_dir"; then
        rm -rf "$tmp_dir"
        return 1
    fi

    mkdir -p "$mod_dir"
    for mod_file in "${MOD_FILES[@]}"; do
        local src="$tmp_dir/$mod_file"
        if [[ ! -f "$src" ]]; then
            src=$(find "$tmp_dir" -name "$mod_file" -type f | head -1)
        fi
        if [[ -n "$src" && -f "$src" ]]; then
            cp "$src" "$mod_dir/$mod_file"
        else
            warn "Mod file not found in archive: $mod_file"
        fi
    done

    rm -rf "$tmp_dir"
    success "Mod installed to $mod_dir"
    return 0
}

# ── Uninstall ──────────────────────────────────────────────────────────────────

do_uninstall() {
    echo ""
    echo -e "${CYAN}  STS2 CLI Mod Uninstaller${NC}"
    echo ""

    detect_platform

    local install_dir
    install_dir=$(resolve_cli_dir)
    local rid
    rid=$(resolve_rid)
    local exe_name
    exe_name=$(resolve_exe_name "$rid")
    local removed=false

    # Remove CLI
    local exe_path="$install_dir/$exe_name"
    if [[ -f "$exe_path" ]]; then
        rm -f "$exe_path"
        success "Removed CLI: $exe_path"
        removed=true
    fi

    # Remove bug reports directory
    local bug_dir="$install_dir/sts2-cli-bugs"
    if [[ -d "$bug_dir" ]]; then
        rm -rf "$bug_dir"
        success "Removed bug reports: $bug_dir"
    fi

    # Remove CLI directory if empty
    if [[ -d "$install_dir" ]] && [[ -z "$(ls -A "$install_dir" 2>/dev/null)" ]]; then
        rmdir "$install_dir" 2>/dev/null || true
        success "Removed empty directory: $install_dir"
    fi

    # Remove PATH/alias from shell configs
    remove_shell_path_and_alias "$install_dir"
    success "Cleaned shell config (PATH/alias)"

    # Remove Mod files
    local game_directory
    game_directory=$(find_game_directory)
    if [[ -n "$game_directory" ]]; then
        local mod_dir="$game_directory/mods"
        for mod_file in "${MOD_FILES[@]}"; do
            local file_path="$mod_dir/$mod_file"
            if [[ -f "$file_path" ]]; then
                rm -f "$file_path"
                success "Removed mod file: $file_path"
                removed=true
            fi
        done
    else
        warn "Could not locate game directory. Mod files may need manual removal."
        muted "Mod files: STS2.Cli.Mod.dll, STS2.Cli.Mod.json in <game>/mods/"
    fi

    echo ""
    if [[ "$removed" == "true" ]]; then
        success "Uninstall complete."
    else
        muted "Nothing to uninstall (STS2 CLI Mod does not appear to be installed)."
    fi
}

# ── Main ───────────────────────────────────────────────────────────────────────

main() {
    echo ""
    echo -e "${BOLD}${CYAN}  ╔═══════════════════════════════════╗${NC}"
    echo -e "${BOLD}${CYAN}  ║       STS2 CLI Mod Installer      ║${NC}"
    echo -e "${BOLD}${CYAN}  ╚═══════════════════════════════════╝${NC}"
    echo ""

    # Handle uninstall
    if [[ "$uninstall" == "true" ]]; then
        do_uninstall
        return
    fi

    detect_platform
    step "Platform: $os/$arch$(if [[ "$is_wsl" == "true" ]]; then echo " (WSL)"; fi)"

    # ── Resolve CLI install directory ──────────────────────────────────────

    local install_dir
    install_dir=$(resolve_cli_dir)

    # ── Resolve version ────────────────────────────────────────────────────

    local release_tag=""
    if [[ -n "$requested_version" ]]; then
        release_tag="v$requested_version"
        step "Using specified version: $release_tag"
    else
        step "Fetching latest release..."
        release_tag=$(get_latest_release_tag) || {
            err "Could not determine latest version."
            muted "Try specifying a version: install.sh -v 0.102.1"
            exit 1
        }
        step "Latest version: $release_tag"
    fi
    local ver="${release_tag#v}"
    echo ""

    # ── Check existing installation ────────────────────────────────────────

    if [[ "$skip_cli" == "false" ]]; then
        local rid
        rid=$(resolve_rid)
        local exe_name
        exe_name=$(resolve_exe_name "$rid")
        local exe_path="$install_dir/$exe_name"
        local installed_ver
        installed_ver=$(get_installed_cli_version "$exe_path")
        if [[ -n "$installed_ver" && "$installed_ver" == "$ver" ]]; then
            muted "CLI version $ver is already installed. Skipping CLI."
            skip_cli=true
        elif [[ -n "$installed_ver" ]]; then
            muted "Upgrading CLI from $installed_ver to $ver"
        fi
    fi

    # ── Detect game directory ──────────────────────────────────────────────

    local game_directory=""
    local mod_dir=""
    if [[ "$skip_mod" == "false" ]]; then
        game_directory=$(find_game_directory)
        if [[ -n "$game_directory" ]]; then
            mod_dir="$game_directory/mods"
            step "Game directory: $game_directory"
        else
            warn "Could not locate $GAME_NAME installation."
            muted "Mod installation will be skipped."
            muted "To install the mod, re-run with: --game-dir \"<path to game>\""
            skip_mod=true
        fi
        echo ""
    fi

    # ── Install CLI ────────────────────────────────────────────────────────

    local cli_ok=false
    if [[ "$skip_cli" == "false" ]]; then
        if install_cli "$release_tag" "$install_dir"; then
            cli_ok=true
            if [[ "$no_modify_path" == "false" ]]; then
                if [[ "$is_wsl" == "true" ]]; then
                    local rid
                    rid=$(resolve_rid)
                    local exe_name
                    exe_name=$(resolve_exe_name "$rid")
                    setup_wsl_alias "$install_dir/$exe_name"
                else
                    if add_to_shell_path "$install_dir"; then
                        success "Added $install_dir to PATH in shell config"
                        muted "Restart your terminal for PATH changes to take effect."
                    fi
                fi
            fi
        fi
        echo ""
    fi

    # ── Install Mod ────────────────────────────────────────────────────────

    local mod_ok=false
    if [[ "$skip_mod" == "false" ]]; then
        if install_mod "$release_tag" "$mod_dir"; then
            mod_ok=true
        fi
        echo ""
    fi

    # ── Summary ────────────────────────────────────────────────────────────

    echo -e "  ${BOLD}── Summary ──${NC}"
    echo ""

    if [[ "$cli_ok" == "true" || "$skip_cli" == "true" ]]; then
        local rid
        rid=$(resolve_rid)
        local exe_name
        exe_name=$(resolve_exe_name "$rid")
        local exe_path="$install_dir/$exe_name"
        if [[ -f "$exe_path" ]]; then
            local ver_output
            ver_output=$("$exe_path" --version 2>/dev/null) || true
            if [[ -n "$ver_output" ]]; then
                success "CLI:  v$ver_output  ($install_dir)"
            else
                success "CLI:  $exe_path"
            fi
        elif [[ "$skip_cli" == "true" ]]; then
            muted "CLI:  skipped"
        fi
    else
        err "CLI:  installation failed"
    fi

    if [[ "$mod_ok" == "true" ]]; then
        success "Mod:  $mod_dir"
    elif [[ "$skip_mod" == "true" ]]; then
        muted "Mod:  skipped"
    else
        err "Mod:  installation failed"
    fi

    echo ""
    echo -e "  ${BOLD}── Get Started ──${NC}"
    echo ""
    muted "1. Launch $GAME_NAME with mods enabled"
    muted "2. Open a terminal and run:"
    echo ""
    echo -e "     ${BOLD}sts2 ping${NC}"
    echo ""
    muted "For more information: https://github.com/$REPO"
    echo ""
}

main
