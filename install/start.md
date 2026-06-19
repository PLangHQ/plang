# Install PLang

PLang is a single command-line tool. Download it, add it to your PATH, and you're done.

---

## Windows

1. Download `plang-windows.zip` from [github.com/PLangHQ/plang/releases](https://github.com/PLangHQ/plang/releases)
2. Unzip to a folder — for example `C:\plang\`
3. Add that folder to your PATH:
   - Press `Win + R`, type `sysdm.cpl`, press Enter
   - Open the **Advanced** tab → **Environment Variables**
   - Find the `Path` variable → **Edit** → **New** → type `C:\plang\`
4. Open a new terminal and verify:

```bash
plang --version
```

---

## macOS

Open Terminal and run:

```bash
# Download — use arm64 for M-series chips, x64 for Intel
wget https://github.com/PLangHQ/plang/releases/latest/download/plang-osx-arm64.zip
unzip plang-osx-arm64.zip -d ~/plang/

# Allow it to run
chmod +x ~/plang/plang
codesign --sign - --force --preserve-metadata=entitlements,requirements,flags,runtime ~/plang/plang

# Add to PATH — paste this into ~/.zshrc or ~/.bash_profile
export PATH=$PATH:$HOME/plang/
```

Then open a new terminal window and verify:

```bash
plang --version
```

---

## Linux

```bash
# Download — use arm64 for ARM machines
wget https://github.com/PLangHQ/plang/releases/latest/download/plang-linux-x64.zip
unzip plang-linux-x64.zip -d ~/plang/

# Set permissions
chmod 700 ~/plang/plang

# Add to PATH — paste into ~/.bashrc or ~/.zshrc
export PATH=$PATH:$HOME/plang/
```

Open a new terminal and verify:

```bash
plang --version
```

---

## Setting up an LLM

Building PLang programs requires an LLM. The first time you run `plang build`, you'll be asked to choose:

- **PLang service** — easiest setup, supports the project
- **Your own OpenAI key** — lower cost, requires an OpenAI account

Building a typical step costs around $0.002–$0.009. Running your compiled program is free.

---

## What's next

- [Start](/) — what PLang is and how it works
- [Goal](/goal/) — the basic building block of a program
