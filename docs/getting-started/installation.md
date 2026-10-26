# Installation

PLang runs on Windows, Linux, and macOS. The pages here describe the current source. The latest published release is `v0.1.17.3`, from February 2025, and it was not run against them, so do not assume it matches. [Build from source](../../doc/install.md) to get the program these pages describe. The release steps below get that older release onto your PATH.

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

There is no `plang --version`: it is refused with `UnknownSetting`. To prove the install, run `plang` in an empty folder. It answers `GoalNotBuilt (404): Goal /Start.goal isn't built; run plang build`, which shows the runtime started and read the folder.

## LLM Setup

Building PLang code needs a TypeSafe key. Give it one of two ways:

- Set the `TYPESAFE_API_KEY` environment variable.
- Run `plang build` and paste the key when it asks (`Building needs a TypeSafe key for the decider. Paste it:`). It is saved, so it is asked once.

With no key and no one to ask (a script, a pipe), the build stops with `MissingDeciderKey` and names both ways. A program that calls an LLM itself (`llm.query`) uses its own key: `%!llm.setting.key%` or `OPENAI_API_KEY`, asked once the same way.

A typical step costs $0.002–$0.009 to build. Running compiled code is free.

## Next

[Write your first PLang program →](hello-world.md)
