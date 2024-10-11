# Get Started with Plang

> [!CAUTION]
> **Heads up: Building code costs money**
> Each code line incurs usually between $0.006 - $0.009 fee via LLM. The payoff? Exceptional efficiency gains. You can choose to use [Plang service(simpler) or OpenAI(cheaper)](./PlangOrOpenAI.md). Using Plang service supports the project

This guide is specifically designed to cater to new Plang users. Whether you're on Windows, Linux, or macOS, this guide will help you navigate through the initial stages of Plang programming with ease. 

We'll go through the following

1. Installing plang
2. Installing development environment (IDE)
3. First plang app, "Hello plang world"

## 1. Install plang
You need to install plang on your system. Refer to the [Installation Guide](Install.md) if you haven't installed it yet. Then come back.

## 2. Integrated development environment (IDE)
Visual Studio Code as your code editor with the Plang extension installed. For more details, refer to the [IDE Setup Guide](Ide.md).

## 3. First plang app, "Hello plang world"


Watch this helpful video tutorial: [Hello World in Plang](https://www.youtube.com/watch?v=iGW4btk34yQ)



1. **Create a Folder**: Make a new folder named `HelloWorld`. For example:
   - Windows: `C:\apps\HelloWorld`
   - Linux/MacOS: `/home/user/plang/HelloWorld`

2. **Create Start.goal File**: In the `HelloWorld` folder, create a file named `Start.goal`.

3. **Write Your Code** 

```plang
Start
- write out 'Hello plang world'
```

4. **Save the File**: After writing the code, save the `Start.goal` file.

5. **Execute the Code**:
   - If you are using VS Code as your editor, then press `F5` to run
   - or run it from the console/terminal with the following command:
     ```bash
     plang exec
     ```
6. **Purchase voucher in plang service** ([see for OpenAI API key](./PlangOrOpenAI.md)):
    - On your first run, you will be asked to fill up your account. 
    - Click the link that is provided
    - Purchase for the amount of your choosing. If you are trying for the first time, we recommend something small, like $5.
    - After purchase, run again 
     ```bash
     plang exec
     ```
7. **Output**: Your code will compile and execute, displaying "Hello plang world" on the screen.


Congratulations! You've now made your first plang app, next let's [create a Todo app](Todo_webservice.md) where you will learn how to talk with a database and  connect to an LLM (AI) service


# [Create a Todo app >>](Todo_webservice.md)