# Plang Settings Documentation

## Introduction

In Plang, settings are a powerful feature that allows you to manage configuration values dynamically. This document will guide you through retrieving, adding, and customizing settings in Plang. Whether you're a beginner or an advanced user, this guide will help you understand how to effectively use settings in your Plang applications.

## Retrieving Settings

To retrieve a setting in Plang, you can use the syntax `%Settings.Name_of_Key%`. For example, to access an API key stored in settings, you would use `%Settings.API_KEY%`. If the setting is not found or is empty, Plang will prompt the user to provide the value.

### Example

```plang
APIRequest
- get http://some-api.com
    Bearer: %Settings.SomeAPIKey%
```

In this example, Plang will attempt to retrieve the `SomeAPIKey` from the settings. If the key is not found, the user will be prompted to enter it.

## Adding New Settings Keys

When you reference a setting like `%Settings.MyKey%` in your Plang code, Plang will search for this key in the settings database located at `.db/system.sqlite`. If the key exists, its value will be returned. If not, Plang will ask the user for the value using the `AskUserHandler`, which can be customized.

To change or remove a key, you will need to open the `.db/system.sqlite` database with a database tool.

## Advanced Customization

For advanced users, Plang allows customization of the settings service. This is useful if you want to use a custom storage medium for settings, such as environment files, cloud secret managers, or other secure storage solutions.

### Overwriting the Settings Service

You can overwrite the default settings service by injecting your own implementation. This is done by implementing the `ISettingsRepository` interface and injecting the DLL.

```plang
Start
- inject settings, 'mysettings', global
```

This command tells the Plang runtime to inject the service located in the `services/mysettings/*.dll` folder and sets it to be global to the application.

### Customizing how it asks user

If you want to customize how Plang asks the user for input, you can inject your own [`AskUserHandler` service](https://github.com/PLangHQ/plang/blob/main/Documentation/Services.md#askuser-service). This is useful if you want to handle user prompts differently.

Let’s say you have a Plang app running on a server. You can configure it to alert your admin if a setting is missing. The app could [send a message](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Messaging.md) or email, then pause while waiting for a response. Once the admin provides the answer, the app updates the setting and resumes from where it left off. 

To do this, implement the `IAskUserHandler` interface and inject it:

```plang
Start
- inject askuser, 'myaskuser'
```

Plang will look for your service in the `services/myaskuser` folder. Your service should implement the following method:

```csharp
public async Task<bool> Handle(AskUserException ex)
```

After processing the logic, call:

```csharp
await ex.InvokeCallback(value);
```

Here, `value` is the expected response from the user. For example, when creating a database connection, the response can be in natural language, and the LLM will map it correctly to establish the connection.

You can check out the [AskUserMessage service](https://github.com/PLangHQ/services/tree/main/PLang.AskUserMessage) already in the git repo


## Further Reading

For more information on creating your own services, refer to the [Services Documentation](./Services.md).

This documentation provides a comprehensive overview of how to manage settings in Plang, from basic retrieval to advanced customization. By following these guidelines, you can effectively manage configuration values in your Plang applications.