# Plang Programming Language Install Guide

Welcome to the Plang programming language install guide. This document will provide you with detailed instructions on how to install Plang on your system, regardless of whether you're using Windows, Linux, or MacOS. By following these steps, you'll be ready to start developing with Plang in no time.

## Table of Contents

1. [Installing Plang on Windows](#installing-plang-on-windows)
2. [Installing Plang on Linux](#installing-plang-on-linux)
3. [Installing Plang on MacOS](#installing-plang-on-macos)
4. [Next Steps](#next-steps)

## Installing Plang on Windows

To install Plang on your Windows machine, follow these steps:

- Navigate to the Plang download page at [http://plang.is/download](http://plang.is/download).
- Download the Plang zip file.
- Unzip the file to a preferred location, such as `C:\plang\`.
- Add `C:\plang\` to your system's PATH environment variable:
  - Right-click on 'This PC' or 'Computer' on your desktop or in File Explorer.
  - Select 'Properties'.
  - Click on 'Advanced system settings'.
  - In the System Properties window, click on the 'Environment Variables...' button.
  - In the Environment Variables window, under 'System variables', find and select the 'Path' variable, then click 'Edit...'.
  - In the Edit Environment Variable window, click 'New' and add `C:\plang\`.
  - Click 'OK' to close each window.

## Installing Plang on Linux

For Linux users, the installation process is as follows:

- Visit [http://plang.is/download](http://plang.is/download) to download the Plang zip file.
- Extract the zip file to a directory of your choice, for example, `/opt/plang/`.
- Add the Plang directory to your PATH environment variable by editing your shell's profile script:
  - Open a terminal.
  - Use a text editor to open your profile script (e.g., `~/.bashrc` for bash or `~/.zshrc` for zsh).
  - Add the following line at the end of the file:

```
export PATH=$PATH:/opt/plang/
```

  - Save the file and close the text editor.
  - Apply the changes by running `source ~/.bashrc` or `source ~/.zshrc`, depending on your shell.

## Installing Plang on MacOS

To install Plang on MacOS, please follow these instructions:

- Go to [http://plang.is/download](http://plang.is/download) and download the Plang zip file.
- Unzip the file to a location such as `/usr/local/plang/`.
- Add the Plang directory to your PATH environment variable:
  - Open the Terminal.
  - Edit your shell profile file (e.g., `~/.bash_profile` for bash or `~/.zshrc` for zsh) using a text editor.
  - Add the following export command to your profile script:

```
export PATH=$PATH:/usr/local/plang/
```

  - Save the changes and close the text editor.
  - To make the changes take effect, run `source ~/.bash_profile` or `source ~/.zshrc`.

## Next Steps

After successfully installing Plang on your system, the next step is to set up your development environment. Please proceed to the [Install Development Environment (IDE) guide](./IDE.md) for detailed instructions on how to configure your IDE for Plang development.

Happy coding with Plang!