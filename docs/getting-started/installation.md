# Installation

PLang runs on Windows, Linux, and macOS. Download the latest release and add it to your PATH.

## Windows

1. Download `plang-windows.zip` from [github.com/PLangHQ/plang/releases](https://github.com/PLangHQ/plang/releases)
2. Unzip to a folder, e.g. `C:\plang\`
3. Add to PATH:
   - Press `Win + R`, type `sysdm.cpl`, Enter
   - Advanced tab → Environment Variables
   - Edit the `Path` variable → add `C:\plang\`

## Linux

```bash
# Download and extract (use arm64 variant for ARM)
wget https://github.com/PLangHQ/plang/releases/latest/download/plang-linux-x64.zip
unzip plang-linux-x64.zip -d ~/plang/

# Set permissions
chmod 700 ~/plang/plang

# Add to PATH (add to ~/.bashrc or ~/.zshrc)
export PATH=$PATH:$HOME/plang/
```

## macOS

```bash
# Download (use x64 for Intel, arm64 for M-series)
wget https://github.com/PLangHQ/plang/releases/latest/download/plang-osx-arm64.zip
unzip plang-osx-arm64.zip -d ~/plang/

# Set permissions and sign
chmod +x ~/plang/plang
codesign --sign - --force --preserve-metadata=entitlements,requirements,flags,runtime ~/plang/plang

# Add to PATH (add to ~/.zshrc or ~/.bash_profile)
export PATH=$PATH:$HOME/plang/
```

## Verify

```bash
plang --version
```

You should see the PLang version number.

## LLM Setup

Building PLang code requires an LLM. On your first build, you'll be prompted to set up payment:

- **PLang service** — simpler setup, supports the project
- **OpenAI API key** — cheaper, requires your own key

A typical step costs $0.002–$0.009 to build. Running compiled code is free.

## Next

[Write your first PLang program →](hello-world.md)
