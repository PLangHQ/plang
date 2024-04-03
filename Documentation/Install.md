# Plang Programming Language Installation Guide

Welcome to the installation guide for the Plang programming language. This guide will walk you through the steps to install Plang on your system. By the end of this guide, you will have Plang installed and ready for use on your Windows, Linux, or MacOS machine.

## Table of Contents

- [Installing Plang on Windows](#installing-plang-on-windows)
- [Installing Plang on Linux](#installing-plang-on-linux)
- [Installing Plang on MacOS](#installing-plang-on-macos)
- [Next Steps](#next-steps)

## Installing Plang on Windows

To install Plang on Windows, follow these steps:

1. Visit the Plang releases page on GitHub at [https://github.com/PLangHQ/plang/releases](https://github.com/PLangHQ/plang/releases).
2. Download the latest release zip file for Windows, plang-windows.zip
3. Unzip the file to your preferred location, such as `C:\plang\`.

### Adding Plang to PATH

To add Plang to your system's PATH environment variable:

1. Press `Win + R`, type `sysdm.cpl`, and press Enter to open System Properties.
2. Go to the Advanced tab and click on 'Environment Variables...'.
3. Under 'System variables', find and select the 'Path' variable, then click 'Edit...'.
4. Click 'New' and add the path where you unzipped Plang, e.g., `C:\plang\`.
5. Click 'OK' to close each window.

### Validate Installation

Open Command Prompt and type:

```bash
plang --version
```

You should see the version of Plang displayed, confirming the installation.

## Installing Plang on Linux

For Linux users, follow these steps:

1. Navigate to [https://github.com/PLangHQ/plang/releases](https://github.com/PLangHQ/plang/releases) and download the latest release zip file for Linux, `plang-linux-x64.zip` or `plang-linux-arm64.zip` for ARM
2. Extract the zip file to a directory within your home folder, for example, `~/plang/`.

### Setting Permissions

Before adding Plang to your PATH, ensure the `plang` binary has the correct permissions:

```bash
chmod 755 ~/plang/plang
```

### Adding Plang to PATH

Add Plang to your PATH environment variable by editing your shell's profile script:

1. Open your profile script with a text editor, e.g., `nano ~/.bashrc` or `nano ~/.zshrc`.
2. Add the following line at the end of the file:

```
export PATH=$PATH:$HOME/plang/
```

3. Save the file and apply the changes by running `source ~/.bashrc` or `source ~/.zshrc`.

### Validate Installation

Open a terminal and type:

```bash
plang --version
```

You should see the version of Plang displayed, confirming the installation.

## Installing Plang on MacOS

To install Plang on MacOS, the steps are similar to Linux:

1. Go to [https://github.com/PLangHQ/plang/releases](https://github.com/PLangHQ/plang/releases) and download the latest release zip file for MacOS, plang-osx-x64.zip or plang-osx-arm64.zip (M1 or M2 CPU)
2. Unzip the file to a location such as `~/plang/`.
3. Run this command to allow plang to run
```bash
chmod +x plang
```
4. Since plang is not signed you need to run following command
```bash
codesign --sign - --force --preserve-metadata=entitlements,requirements,flags,runtime ./plang
```


### Adding Plang to PATH

Add Plang to your PATH environment variable:

1. Open your shell profile file with a text editor, e.g., `nano ~/.bash_profile` or `nano ~/.zshrc`.
2. Add the following export command to your profile script:

```
export PATH=$PATH:$HOME/plang/
```

3. Save the changes and run `source ~/.bash_profile` or `source ~/.zshrc` to apply them.

### Validate Installation

Open the Terminal and type:

```bash
plang --version
```

You should see the version of Plang displayed, confirming the installation.

## Next Steps

With Plang installed, you're ready to set up your development environment. Proceed to the [Install Development Environment (IDE) guide](./IDE.md) for instructions on configuring your IDE for Plang development.