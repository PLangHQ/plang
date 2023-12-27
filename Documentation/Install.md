### Installing PLang

#### Introduction
This guide provides step-by-step instructions for installing the PLang executable on different operating systems (Windows, Linux, macOS).

#### Installation Process

##### Windows
1. **Download Executable**: [Download PLang for Windows](https://github.com/PLangHQ/plang/plang.exe)
2. **Install PLang**: Follow the instructions in the [installation video](https://www.youtube.com/watch?v=jYEqoIeAoBg).
3. **Add to PATH**:
   - Access System Properties > Advanced > Environment Variables.
   - Edit the 'Path' variable to include the PLang directory.

##### macOS
1. **Download Executable**: [Download PLang for macOS](https://github.com/PLangHQ/plang/plang)
2. **Install PLang**: Open Terminal and navigate to the download directory.
3. **Add to PATH**:
   - Open Terminal.
   - Run `export PATH=$PATH:/path/to/plang`.

##### Linux
1. **Download Executable**: [Download PLang for Linux](https://github.com/PLangHQ/plang/plang)
2. **Install PLang**: Use Terminal to navigate to the download directory.
3. **Add to PATH**:
   - Open Terminal.
   - Edit `~/.bashrc` or `~/.zshrc` to add `export PATH=$PATH:/path/to/plang`.

#### Using PLang in Console/Terminal
- **Build PLang Code**: `plang build`
- **Run PLang Code**: `plang run` (runs `Start.goal` by default)
  - To run a specific goal file: `plang run GoalName`
- **Execute PLang Code**: `plang exec` (builds then runs)

This guide aims to assist beginners in installing and getting started with PLang on their preferred operating system.