# Plang Settings Management Guide

## Introduction

Welcome to the Plang Settings Management Guide. This document is designed to help you understand how to effectively manage and utilize settings within your Plang applications. Settings are a fundamental aspect of Plang, allowing you to define and retrieve configuration values that are essential for your application's functionality.

## Accessing Settings

In Plang, settings are accessed using the `%Settings.Name_of_Key%` syntax. This enables you to fetch the value associated with a specific key within your code. For instance, to retrieve an API key, you would use `%Settings.API_KEY%`.

Should a setting be undefined, Plang will prompt you to input a value for it. Here's an example of how to incorporate a setting within a goal:

```plang
APIRequest
- get http://some-api.com
    Bearer: %Settings.SomeAPIKey%
```

## Defining New Settings Keys

Introducing a new settings key is as simple as referencing it in your Plang code. For example, `%Settings.MyKey%` will prompt Plang to search for this key in the settings database, which is located at `.db/system.sqlite`. If the key is found, its value will be utilized. If not, Plang will request the value from the user via the `AskUserHandler`. This process can be customized, as detailed in the advanced section below.

To alter or remove an existing key, you'll need to use a database tool to interact with the `.db/system.sqlite` database.

## Advanced Customization

### Customizing the Settings Service

For those who require a custom settings service, perhaps to integrate with different storage solutions like .env files or cloud-based secret managers, you can override the default service. This involves implementing the `ISettingsRepository` interface and injecting your custom service into the Plang runtime:

```plang
Start
- inject settings, 'mysettings', global
```

This command directs Plang to use the service from the `services/mysettings/*.dll` directory and sets it as the global settings service for your application.

### Customizing the AskUser Service

If you need to tailor the user interaction when a setting is missing, you can implement your own `AskUser` service. This is useful for integrating with messaging modules or providing a unique user experience. To do this, adhere to the `IAskUserHandler` interface and inject your custom service:

```plang
Start
- inject askuser, 'myaskuser'
```

Plang will then utilize your custom service from the `services/myaskuser` directory.

## Conclusion

This guide has walked you through the essentials of managing settings in Plang. While the default settings service is adequate for most users, Plang's flexibility allows for customization to meet specific needs. Whether you're a beginner or an advanced user, understanding how to manage settings is key to building robust Plang applications.