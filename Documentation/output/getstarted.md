# Getting Started with Plang

## Introduction

Welcome to the Plang programming language! This guide will help you create your first Plang application, "Hello World". 

Before you start, make sure you have installed Plang. If you haven't, please refer to the [Installation Guide](Install.md).

## Prerequisites

- Plang installed on your system. Refer to the [Installation Guide](Install.md) if you haven't installed it yet.
- Visual Studio Code as your code editor with the Plang extension installed. For more details, refer to the [IDE Setup Guide](Ide.md).

## Creating a "Hello World" Application

You can follow along with this [video tutorial](https://www.youtube.com/watch?v=iGW4btk34yQ) for a more detailed walkthrough.

If you're using Plang for the first time, you'll need to set up your Plang service account. This is a straightforward process and supports Plang directly. Alternatively, you can use a GPT-4 API key, which is a bit more complex but cheaper. For more information, refer to [PlangOrOpenAI.md](PlangOrOpenAI.md).

### Step 1: Create a Project Directory

Create a new directory named `HelloWorld` at your preferred location. For instance, you can create it at `c:\plang\HelloWorld`.

### Step 2: Create `Start.goal` File

In the `HelloWorld` directory, create a new file named `Start.goal`.

### Step 3: Write the Code

Open `Start.goal` in Visual Studio Code and write the following code:

```plang
Start
- write out 'Hello plang world'
```

### Step 4: Save the File

Save the `Start.goal` file.

### Step 5: Build and Run the Application

To build and run the application, you can either press `F5` in Visual Studio Code or run the following command in your terminal:

```bash
plang exec
```

This command will build your code and then run it, printing out "Hello plang world" to the console.

Congratulations! You have successfully created your first Plang application.

For the next steps, you can learn how to create a Todo web service in Plang by following the [Todo Web Service Guide](todo_webservice.md).