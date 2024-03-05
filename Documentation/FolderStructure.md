# plang Project Folder Structure Guide

This guide provides a comprehensive overview of the folder structure for a plang project. By adhering to this structure, you will maintain a well-organized codebase, making it easier for you and your team to develop, maintain, and scale your plang applications.

## Root Folder

The root folder is the heart of your plang project. It's where you'll find the `Start.goal` and `Setup.goal` files, which are crucial for bootstrapping and configuring your application.

### Key Directories in the Root Folder

- **.build**: This directory is reserved for files that are part of the build process.
- **.db**: This directory is crucial for your application's data management. It contains:
  - `system.sqlite`: The main database file for system-related data.
  - `data.sqlite`: This file is generated when your application defines tables and needs a separate database for application data.

For instance, if your plang project is located at `c:\apps\TestApp`, the `TestApp` directory is your root folder.

## API Folder

The `api` folder is the default location for your application's RESTful services. While this is the standard, you can modify the API service path when you launch the web server.

### Public Goals in the API Folder

Public goals are the first goal in each file within the `api` folder and serve as the entry points for your API services. They include API-related specifications such as:

- HTTP method (e.g., GET, POST)
- Content encoding
- Content type
- Maximum content length
- Caching control

These specifications are included in the goal name, as shown in the following example:

```plang
GetList - POST, max length=1mb, public cache.
```

## UI Folder

The `ui` folder is dedicated to the User Interface (UI) components of your application. It focuses on the data availability and structure without specifying the device type for the UI.

By default, plang will construct the UI using HTML, JavaScript, and CSS, leveraging the Bootstrap framework for styling. Additionally, Font Awesome is included for icons.

## Events Folder

The `events` folder is where you'll place scripts for events that are triggered within the application. Detailed information about these events can be found in the [Events Documentation](./Events.md).

## Modules Folder

The `.modules` folder is where you store `.dll` files that you want to include in your build process. This enables you to enhance plang with new features. For further details, refer to the [Modules Documentation](./modules/README.md).

## Services Folder

The `.services` folder is designed for overriding the base functionality of the plang language. Whether you need to modify the Low-Level Memory (LLM), caching, database, or other aspects, you can place the corresponding `.dll` in this folder and integrate it into your code. For more information, see the [Services Documentation](./Services.md).

By following this folder structure, you will ensure that your plang project is organized in a manner that promotes efficiency and clarity, which is essential for successful development and collaboration.