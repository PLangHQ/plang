### Services in PLang

Services in PLang are powerful tools that extend its functionality through dependency injection. Developers can customize PLang by injecting various service types, enhancing its capabilities to suit specific needs.

> Note: More services will be added in the future. This implentation came late in development.

#### List of Services
- **db**: Database connections.
- **settings**: Configuration management.
- **caching**: Data caching functions.
- **logger**: Logging services.
- **llm**: Large Language Model integration.
- **askuser**: User interaction handling.
- **encryption**: Data encryption and decryption.
- **archiver**: File compression and decompression.

#### Services Location
Services must be located under the `services` folder in your folder where your Start.goal and Setup.goal files are located.

#### Detailed Service Implementation

##### db Service
You can download nuget package for the database you need support for. Find the package and click Download package, unzip the package (see instruction at bottom). 

The unzipped should be located in /lib/.net8.0/NameOfDll.dll, put it into `services` folder that fits you.

```plang
- inject db, /npgsql/lib/net8.0/Npgsql.dll, global
- inject db, /MySqlConnector/lib/net8.0/MySqlConnector.dll
- inject db, SqlConnector
```

When you create a datasource in your Setup.goal file, the plang language will ask you for the information to connect to the database.

```plang
Setup
- Create datasource 'TheProductionDb'
```

```plang
Start
- Set data source 'TheProductionDb'
```

#### Nuget locations
- [Postgresql](https://www.nuget.org/packages/Npgsql)
- [MySql/MariaDb](https://www.nuget.org/packages/MySqlConnector)
- Microsoft SQL Server - already available in plang

Any library that implements IDbConnection should work.

##### settings Service

This allows you to change where the settings are stored. You need to implement the [ISettingsRepository](https://github.com/PLangHQ/plang/blob/main/PLang/Interfaces/ISettingsRepository.cs). 

This will allow you to create implementation to read .env files, Azure Vault, AWS Secret manager, Google Cloud Secret Manager or other storages for sensitive data.

```plang
- inject settings, /environment_vars/myimplementation.dll
```
##### caching Service

Plang uses in memory caching. For services that need distributed caching, you can implement [IAppCache](https://github.com/PLangHQ/plang/blob/main/PLang/Interfaces/IAppCache.cs)

```plang
- inject caching, /mycaching/mycaching.dll
```

##### llm Service

You can overwrite the LLM service that comes with plang. There is already [OpenAIService](https://github.com/PLangHQ/services/blob/main/OpenAiService/OpenAiService.cs) that you can use instead of the built in LLM plang service (Note: plang uses OpenAI but you dont need API key).

You need to implement the [ILlmService](https://github.com/PLangHQ/plang/blob/main/PLang/Interfaces/ILlmService.cs) interface

```plang
- inject llm, /myllm/myll.dll
```

If you want to build your code using your own LLM service, you MUST inject it like this in you Start.goal or Events.goal.

```plang
@llm=/myllm/myll.dll
```

##### askuser Service

The plang language asks the user for information when it needs it. You need to implement the [IAskUserHandler](https://github.com/PLangHQ/plang/blob/main/PLang/Interfaces/IAskUserHandler.cs).

```plang
- inject askuser, /myAskUser/myAskUser.dll
```

##### encryption Service

The plang language uses AES256 for encryption. The private key is stored using the ISettingsRepository in the default implementation. 

You can change the implementation by implementing the [IEncryption](https://github.com/PLangHQ/plang/blob/main/PLang/Interfaces/IEncryptionService.cs) interface. This way you can choose your own encryption standard or your own storage medium.

```plang
- inject encryption, /myEncryption/myEncryption.dll
```

##### archiver Service

Archiver service handles compression and decomression in the plang language. Plang language uses zip compression.

```plang
- inject archiver, /myarchiver/myarchiver.dll
```

##### logger Service

You can overwrite the logger in the language by implementing the Microsoft.Extensions.Logging.ILogger interface.

```plang
- inject logger, /MyLogger/MyLogger.dll
```

### Implementing a Custom Service
A step-by-step guide to creating a custom service. For example, creating a custom logger:

```csharp
public class MyLogger<T> : ILogger<T>
{
     public virtual void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Custom logging logic
    }
}
```

### Using NuGet Packages in PLang

PLang supports the integration of NuGet libraries, especially those implementing service contracts like `IDbConnection`. To use a NuGet library in PLang:

1. **Download the Package**: Visit the NuGet library page and click "Download Package" to receive a `.nuget` file.
2. **Convert to Zip**: Rename the downloaded `.nuget` file to `.zip`.
3. **Unzip and Place in Services Folder**: Unzip the file and place its contents into the PLang `services` folder.
4. **Reference in PLang**: Point to the unzipped library in your PLang `inject` step.

This process allows you to leverage the vast array of existing libraries in NuGet for enhancing your PLang applications.
