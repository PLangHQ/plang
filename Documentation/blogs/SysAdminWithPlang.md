# How I Use Plang for System Administration

Plang is a powerful tool, especially when it comes to system administration. Its ability to operate at the programming layer while leveraging various tools gives developers—and sysadmins —an impressive level of control and efficiency.

In system administration, scripts simplify repetitive tasks, and I use Plang specifically to deploy apps to servers. My Plang apps are pretty straightforward, so they don’t need the heavy-duty CI/CD pipeline, which would just add unnecessary complexity.

So, how does it work?

## The Plan

My goal is to take the `app` folder from my development machine and set it up on a Linux server. Here’s what needs to happen:

1. Zip the `app` folder to `app.zip`, excluding any `.db` and `*.sqlite` files.
2. Connect to my Linux server via SFTP.
3. Upload `app.zip`.
4. Clear out any old `.build` files to avoid using outdated code.
5. Unzip `app.zip` on the server.
6. Set the correct file permissions.
7. Optionally update the Plang runtime.
8. Confirm the Plang runtime is working.

When you’re doing this setup for the first time, it's common to miss a step or two—don’t worry. The flexibility of Plang makes it easy to adjust on the fly.

## The Deployment

Let’s walk through the deployment by setting up a `Deploy.goal` file on my computer, and the first line is simply `Deploy`:

```plang
Deploy
```

### Zipping the App

We’ll start by creating a zip file of the app directory, which is in the same location as `Deploy.goal`:

```plang
- zip "./" to "deploy/app.zip", exclude ".db", "*.sqlite", overwrite file
```

### Connecting via SFTP

Next, I connect to the server using SFTP. I’ve set up a private key with a passphrase for secure access:

```plang
- read file '/events/key', into %sshPrivateKey%
- connect to sftp, 
    host: 185.248.121.67, port: 28781, username: root, 
    private key: %sshPrivateKey%, private key passphrase: %Settings.PrivateKeyPassphrase%
```

Now that we’re logged into the server, we can start deploying.

### Uploading and Setting Up

First, let’s upload the zip file:

```plang
- upload 'deploy/app.zip' through sftp to '/home/plang/app.zip'
```

After that, we’ll run a few Linux commands to clean up any previous `.build` folder and unzip the new code:

```plang
- run ssh command, "rm -rf /home/plang/app/.build"
- run ssh command, 'unzip -o /home/plang/app.zip -d /home/plang/app'
```

### Updating the Plang Runtime

For flexibility, I’ve added an option to update the Plang runtime. If I run the script with `plang=1`, it triggers the update:

```plang
- if %plang% = 1 then SetupPlang
```

Now, let’s create the `SetupPlang` goal.

### The SetupPlang Goal

The `SetupPlang` goal takes care of a few critical steps:

1. Downloads the latest version of Plang for Linux.
2. Unzips `plang.zip`.
3. Sets execution permissions for `plang` and `selenium-manager` (I use a browser in my app).
4. Runs `plang --version` to validate the installation and manages any errors (like creating a symlink or installing i18n).
5. Restarts the service if it’s running or notifies me if it isn’t installed.

```plang
SetupPlang
- run ssh command, 'wget -O /home/plang/plang.zip https://github.com/PLangHQ/plang/releases/latest/download/plang-linux-x64.zip'
- run ssh command, 'unzip -o /home/plang/plang.zip -d /home/plang/plang'
- run ssh command, 'chmod +x /home/plang/plang/plang'
- run ssh command, 'chmod +x /home/plang/plang/selenium-manager/linux/selenium-manager'
- run ssh command, 'plang --version'
    on error message 'No such file or', call CreateSymbolicLink then retry 
    on error message 'command not found', call CreateSymbolicLink then retry 
    on error message contains 'Couldn't find a valid ICU package', call InstallICU then retry
- run ssh command, 'sudo systemctl restart plang', write to %serviceRestart%
    on error message  'plang.service not found', call ServiceNotInstalled ignore error
- write out %serviceRestart%
```

In the case of errors, I have additional commands to handle them:

```plang
CreateSymbolicLink
- write out 'Creating symbolic link'
- run ssh command, 'sudo ln -s /home/plang/plang/plang /usr/local/bin/plang' 
        on error  message contains 'File exists', ignore the error 

InstallICU
- write out 'Installing ICU'
- run ssh command 'sudo dnf install -y libicu'

ServiceNotInstalled
- write out 'Plang service not installed'
```

Finally, to confirm the Plang runtime is installed, I run:

```plang
- run ssh command, 'plang --version', write to %plangVersion%
- write out "App updated at %Now% - %plangVersion%"
```

## Running and Updating

Before building, you’ll need to set up the `SshModule` in your `.module` folder. Head over to [SshModule on GitHub](https://github.com/PLangHQ/modules/tree/main/SshModule), build the project, and copy the `.dll` and `.deps` files into your `.module` folder (if you don’t have one yet, just create it).

Once the code is ready, I build it:

```bash
plang build
```

And then, to deploy both the app and Plang runtime, I run:

```bash
plang Deploy plang=1
```

If I only need to update the app, I just execute:

```bash
plang Deploy
```

Here’s an example of the output:

```
App updated at 29.10.2024 10:57:05 - plang version: 0.15.3.0
```

### Full Code for Reference

```plang
Deploy
- zip "./" to "deploy/app.zip", exclude ".db", "*.sqlite", overwrite file
- read file '/events/key', into %sshPrivateKey%
- connect to sftp, 
    host: myhost.server.com, port: 22, username: root, 
    private key: %sshPrivateKey%, private key passphrase: %Settings.PrivateKeyPassphrase%
- upload 'deploy/app.zip' through sftp to '/home/plang/app.zip'
- run ssh command, "rm -rf /home/plang/app/.build"
- run ssh command, 'unzip -o /home/plang/app.zip -d /home/plang/app'
- if %plang% = 1 then SetupPlang
- run ssh command, 'plang --version', write to %plangVersion%
- write out "App updated at %Now% - %plangVersion%"

SetupPlang
- run ssh command, 'wget -O /home/plang/plang.zip https://github.com/PLangHQ/plang/releases/latest/download/plang-linux-x64.zip'
- run ssh command, 'unzip -o /home/plang/plang.zip -d /home/plang/plang'
- run ssh command, 'chmod +x /home/plang/plang/plang'
- run ssh command, 'chmod +x /home/plang/plang/selenium-manager/linux/selenium-manager'
- run ssh command, 'plang --version'
    on error message 'No such file or', call CreateSymbolicLink then retry 
    on error message 'command not found', call CreateSymbolicLink then retry 
    on error message contains 'Couldn't find a valid ICU package', call InstallICU then retry
- run ssh command, 'sudo systemctl restart plang', write to %serviceRestart%
    on error message  'plang.service not found', call ServiceNotInstalled ignore error
- write out %serviceRestart%    

CreateSymbolicLink
- write out 'Creating symbolic link'
- run ssh command, 'sudo ln -s /home/plang/plang/plang /usr/local/bin/plang' 
        on error  message contains 'File exists', ignore the error 

InstallICU
- write out 'Installing ICU'
- run ssh command 'sudo dnf install -y libicu'

ServiceNotInstalled
- write out 'Plang service not installed'
```

Using Plang in system administration is a real timesaver, making deployment as efficient and straightforward as possible.

## Benefits

Using Plang for deployment keeps things simple and readable, cutting down on complexity while making it easier to manage. With Plang’s clear, natural syntax, I can lay out each deployment step almost as if I’m writing in plain language, which makes everything easy to follow and adjust when needed. The modular setup lets me break tasks down, making it easier to tweak specific steps and handle errors. Plang’s built-in retry and error-handling functions add stability, catching issues automatically when they come up. 

With flexible variable management, Plang also lets me store command outputs for later use, which is super handy for connecting steps and external systems. Plus, if I need to keep track of metadata or deployment logs, Plang’s built-in database support has that covered without any extra setup. Overall, Plang simplifies deployment scripts with smart, high-level abstractions, trimming down the code compared to traditional scripting languages and saving me serious development time.

## More Information

Interested in learning more about Plang? Here are some useful resources to get started:

- [Basic Concepts and Lessons](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Lesson%202.md)
- [Todo Example](https://github.com/PLangHQ/plang/blob/main/Documentation/Todo_webservice.md) to build a simple web service
- Explore the [GitHub repo](https://github.com/PLangHQ/) for source code
- Join our [Discord Community](https://discord.gg/A8kYUymsDD) for discussions and support
- Or chat with the [Plang Assistant](https://chatgpt.com/g/g-Av6oopRtu-plang-help-code-generator) to get help with code generation