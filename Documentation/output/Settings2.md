# Plang Settings Documentation

Welcome to the Plang settings documentation! This guide will help you understand how to manage and utilize settings within your Plang applications. 

## Retrieving Settings

In Plang, settings are key-value pairs that can be accessed throughout your code. To retrieve a setting, you use the `%Settings.Name_of_Key%` syntax, where `Name_of_Key` is the identifier for the setting you wish to access. For example, if you want to retrieve an API key, you would use `%Settings.API_KEY%`.

If a setting is not set, Plang will prompt you to provide a value for it. Here's an example of how you might use a setting within a goal:

```plang
APIRequest
- get http://some-api.com
    Bearer: %Settings.SomeAPIKey%
```

## Overwriting the Settings Service

Plang allows you to customize the settings service by injecting your own implementation. To do this, you must implement the `ISettingsRepository` interface. Once your custom settings service is ready, you can inject it into your Plang application using the following syntax:

```plang
Start
- inject settings, 'mysettings', global
```

This command instructs the Plang runtime to use the service located in the `services/mysettings/*.dll` directory and sets it as the global settings service for the application.

## Custom AskUser Service

Plang has a built-in mechanism to interact with the user through the `AskUserException`. If you want to customize this interaction, for instance, to send a message using the message module, you need to implement the `IAskUserHandler` interface.

To inject your custom `AskUser` service, use the following code in your Plang application:

```plang
Start
- inject askuser, 'myaskuser'
```

The runtime will look for your custom service in the `services/myaskuser` directory.

## Summary

By following the instructions in this documentation, you can effectively manage settings within your Plang applications. Remember to use the `%Settings.Key%` syntax to access your settings, and don't hesitate to create custom services for settings and user interactions to suit your specific needs. Whether you're on Windows, Linux, or macOS, these principles apply and will help you build robust Plang applications. Happy coding!