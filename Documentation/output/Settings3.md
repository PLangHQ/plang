# Plang Settings Management Guide

Welcome to the Plang Settings Management Guide. This document is designed to help developers understand how to work with settings in Plang.

## Accessing Settings

In Plang, settings are stored as key-value pairs and can be accessed using a specific syntax. To retrieve a setting, you use the `%Settings.Name_of_Key%` format. For instance, to access an API key, you would use `%Settings.API_KEY%`.

If a setting is not already defined, Plang will prompt you to provide a value for it. Here's an example of how to use a setting within a goal:

```plang
APIRequest
- get http://some-api.com
    Bearer: %Settings.SomeAPIKey%
```

## Adding New Settings Keys

When you reference a new setting in your Plang code, such as `%Settings.MyKey%`, Plang will search for this key in the settings database located at `.db/system.sqlite`. If the key is found, its value is returned. If not, Plang will ask the user for the value using the `AskUserHandler`, which can be customized (see the section on customizing the AskUser service below).

To modify or remove a key, you will need to use a database tool to open the `.db/system.sqlite` database.

## Customizing the Settings Service

You have the option to override the default settings service with your own implementation. To do this, you need to implement the `ISettingsRepository` interface. After creating your custom settings service, you can inject it into your Plang application with the following code:

```plang
Start
- inject settings, 'mysettings', global
```

This command directs the Plang runtime to use the service located in the `services/mysettings/*.dll` directory and sets it as the global settings service for the application.

## Customizing the AskUser Service

Plang uses the `AskUserException` to interact with the user. If you wish to customize this interaction, for example, to integrate with a messaging module, you need to implement the `IAskUserHandler` interface.

To inject your custom `AskUser` service, use the following code:

```plang
Start
- inject askuser, 'myaskuser'
```

The runtime will then utilize your custom service from the `services/myaskuser` directory.

## Summary

This guide has covered the basics of managing settings in Plang, including how to access, add, and customize settings. 

Happy coding with Plang!