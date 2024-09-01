# Folder Structure Documentation for Plang

This documentation provides an overview of the folder structure used in a Plang project. Understanding this structure is crucial for organizing your project files and ensuring that your application runs smoothly.

## Root Folder

The root folder is the main directory where your `Start.goal` and `Setup.goal` files are located. It serves as the entry point for your Plang application. For example, if you create your Plang project in `C:\apps\TestApp`, then `TestApp` is your root folder.

### Contents of the Root Folder

- **.build Folder**: This folder is automatically generated and used for build-related files.
- **.db Folder**: This folder contains the database files:
  - `system.sqlite`: The main system database.
  - `data.sqlite`: This file is created only if your application includes tables.

## API Folder

The `api` folder contains the REST services for your application. By default, this is where your API-related goals are stored, but this can be changed when starting the web server.

### Public Goals in the API Folder

Public goals are the first goal in each file within the `api` folder. They contain API-related information such as the HTTP method, content encoding, content type, maximum content length, and caching control. These settings can be specified in the goal name. For example:

```plang
GetList - POST, max length=1mb, public cache
```

This line indicates that the `GetList` goal uses the POST method, has a maximum content length of 1MB, and uses public caching.

## UI Folder

The `ui` folder contains files related to the user interface. It defines what data should be available and its structure, but not the specific device the UI is applied to. By default, Plang builds the UI using HTML, JavaScript, and CSS, leveraging the Bootstrap framework. Font Awesome is also available for use.

## Events Folder

The `events` folder contains [events that should run](./Events.md) in the application. This is where you define the event-driven logic for your application.

## .modules Folder

The `.modules` folder is used to include `.dll` files that you want to incorporate into your build process. This allows you to extend Plang with new features. For more information, refer to the [modules documentation](./modules/README.md).

## .services Folder

The `.services` folder allows you to override the base functionality of the Plang language. If you need to change the language model (LLM) used, caching, database, etc., you can use this folder to drop in the necessary `.dll` files and inject them into your code. For more details, see the [services documentation](./Services.md).

By understanding and utilizing this folder structure, you can effectively organize and manage your Plang projects, ensuring a clean and efficient development process.