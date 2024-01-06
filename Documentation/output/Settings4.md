# Plang Settings Management Guide

## Introduction

This guide provides a comprehensive overview of managing settings within the Plang programming language. Settings in Plang are crucial for defining and accessing various configuration values that are essential for the operation of your Plang applications.

## Retrieving Settings

Settings in Plang are accessed using the `%Settings.Name_of_Key%` syntax. This allows you to retrieve the value associated with a specific key. For example, to access an API key, you would use `%Settings.API_KEY%`.

If a setting is not set, Plang will prompt you to provide a value for it. Below is an example of how to use a setting within a goal:

```plang
APIRequest
- get http://some-api.com
    Bearer: %Settings.SomeAPIKey%
```

## Adding New Settings Keys

To add a new settings key, simply reference it in your Plang code as `%Settings.MyKey%`. Plang will then look for this key in the settings database located at `.db/system.sqlite`. If the key exists, its value will be used; if not, Plang will request the value from the user through the `AskUserHandler`. This handler can be customized, which is explained in the advanced section below.

To modify or delete a key, you will need to use a database tool to interact with the `.db/system.sqlite` database.

## Advanced Customization

### Customizing the Settings Service

For advanced users who need to customize the settings service, you can override the default service by implementing the `ISettingsRepository` interface. After creating your custom settings service, you can inject it into your Plang application using the following code:

```plang
Start
- inject settings, 'mysettings', global
```

This command instructs the Plang runtime to use the service located in the `services/mysettings/*.dll` directory and sets it as the global settings service for the application.

### Customizing the AskUser Service

To customize the user interaction when a setting is not found, you can implement your own `AskUser` service by adhering to the `IAskUserHandler` interface. This is particularly useful if you want to integrate with a messaging module or provide a different user experience.

To inject your custom `AskUser` service, use the following code:

```plang
Start
- inject askuser, 'myaskuser'
```

The runtime will then look for your custom service in the `services/myaskuser` directory.

## Conclusion

This guide has introduced you to the basics of settings management in Plang, including how to retrieve, add, and customize settings. For most users, the default settings service will suffice, but for those requiring more control, Plang provides the flexibility to customize services to fit your needs.