# Plang Settings Documentation

## Introduction

In Plang, settings are a powerful feature that allows you to manage configuration values dynamically. This document will guide you through retrieving, adding, and customizing settings in Plang. Whether you're a beginner or an advanced user, this guide will help you understand how to effectively use settings in your Plang applications.

## The goal for setting

The goal of Plang is to avoid the nightmare of complex configuration setups. Instead of getting lost in layers of settings, plang tries to streamline the process. For example, if your Plang app detects a missing setting, it can notify the admin, wait for input, and automatically apply the update—keeping things simple and running smoothly without endless configuration headaches.

## Retrieving Settings

To retrieve a setting in Plang, you can use the syntax `%Settings.Name_of_Key%`. For example, to access an API key stored in settings, you would use `%Settings.API_KEY%`. If the setting is not found or is empty, Plang will prompt the user to provide the value.

### Example

```plang
APIRequest
- get http://some-api.com
    Bearer: %Settings.Some_API_Key%
```

In this example, Plang will attempt to retrieve the `Some_API_Key` from the settings. If the key is not found, the user will be prompted to enter it.

## Adding New Settings Keys

When you reference a setting like `%Settings.MyKey%` in your Plang code, Plang will search for this key in the settings database located at `.db/system.sqlite`. If the key exists, its value will be returned. If not, Plang will ask the user for the value using the `AskUserHandler`, which can be customized.

To change or remove a key, you will need to open the `.db/system.sqlite` database with a database tool.

## A missing setting when no one is there

The question is asked only when someone can answer it.

- **In a web request, a scheduled job or a goal called with `dont wait`** a missing setting is an error, `MissingSetting` (500):
  "The setting production.X has no value". The step stops, and the error goes to the app's error handler like any other. Nothing
  is asked and nothing is stored.
- **At the command line with stdin closed** (a script, `< /dev/null`) the question gets an empty answer: the step goes on with an
  empty value, the run does not fail, and an empty row is stored, so the setting looks "set" from then on. A real value has to
  overwrite it later.

A setting that may be absent gets a default in the step. Then nothing is asked and nothing is stored, wherever it runs:

```plang
- set %pageSize% = %Settings.PageSize%, default is "20"
```

Otherwise never rely on a setting being absent:

- A test instance sets a dummy for **every** `%Settings.X%` the app reads ([Mocking a provider](./TalkingToAnApi.md#mocking-a-provider-for-a-test-instance)).
- A deploy check compares the `%Settings.X%` names used in the goals with `select Key from Settings` and fails by name for any
  that is missing ([Deploying a new version](./Deploy.md)).

## A secret stays in `%Settings.X%`

Write `%Settings.X%` in the step that uses it. Never copy it into a variable first:

```plang
- get https://api.example.com/servers, headers {"Authorization": "Bearer %Settings.ApiToken%"}, write to %servers%
```

When that step fails, the error shows the step as written, `Bearer %Settings.ApiToken%`, and never the value.

A setting's value is **sensitive**: wherever it is copied to (`set %token% = %Settings.ApiToken%`, `"Bearer %token%"`), it is
hidden as `****` and the last 4 characters (`****` when shorter than 12) in an error (`%!error%`, "Variables in step"), the log
and a request to the llm. Modules get the real value. Mark any other value the same way with `as sensitive`:

```plang
- set %password% = %request.password% as sensitive
```

What the app writes out is its own output and gets the real value: `write out` (console and web response), render, files,
http requests, a process and an iroh peer. Only errors, the log and llm requests hide it. Values shorter than 6 characters
are not hidden.

The name of a setting can have a variable in it, to pick one of several keys without the key going into a variable:

```plang
- set %project% = "Finland"
- get https://api.example.com/servers, headers {"Authorization": "Bearer %Settings.Token%project%%"}, write to %servers%
```

`%Settings.Token%project%%` reads `Settings.TokenFinland`. The variable's value may only hold letters, digits and `_`;
anything else is an error.

## The environment is part of the key

`set environment "production"` makes `%Settings.X%` read the row `production.X`; `dev` reads `dev.X`. `production` is the default.
A copy of the production `system.sqlite` has `production.` rows only, so a test instance that runs as `dev` finds none of them. Run a
test instance with the same environment as production.

## Two different rows can share a name

`%Settings.OpenAiKey%` is a row owned by the settings class. The runtime's own llm service reads its own row, owned by the service,
under the same key name. Setting one does not change the other. To block or replace the key for `[llm]` steps, change the service's
row, not only `%Settings.OpenAiKey%`.

## The setup ledger lives here too

The list of setup steps that have already run is a row in the same file (`production.SetupRunOnce`, per environment). A copy of
`system.sqlite` carries it, so a test copy does not repeat setup that production already ran. See [Setup and migrations](./SetupAndMigrations.md).

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