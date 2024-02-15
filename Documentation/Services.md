# Services in PLang

Services in PLang are powerful tools that extend its functionality through dependency injection. Developers can customize PLang by injecting various service types, enhancing its capabilities to suit specific needs.

> Note: More services will be added in the future. This implentation came late in development.

## List of Services
- **db**: Database connections.
- **settings**: Configuration management.
- **caching**: Data caching functions.
- **logger**: Logging services.
- **llm**: Large Language Model integration.
- **askuser**: User interaction handling.
- **encryption**: Data encryption and decryption.
- **archiver**: File compression and decompression.

## Services Location
Services must be located under the `.services` folder in your folder where your Start.goal and Setup.goal files are located.

## Location of injection code

To inject a library you use the syntax (approx.)
```plang
- inject db, /npgsql/lib/net8.0/Npgsql.dll
```
The only location you should NOT put this, is in the Setup.goal. This is because each step in Setup.goal is only executed one time. Not each time on startup, but one time of the whole lifetime (years?) of your app.

If you want to have this accessable throughout your app, then Start.goal or Events.goal would be the place to put it. If you only need to use the library in specific goal, you can include it in that.


## Detailed Service Implementation

### db Service
You can download nuget package for the database you need support for. [Find the package on nuget.org](https://www.nuget.org/) and click Download package, unzip the package (see instruction at bottom). 

The unzipped file should be located in /lib/.net8.0/NameOfDll.dll, put it into  folder that fits you.

```plang
- inject db, /npgsql/lib/net8.0/Npgsql.dll
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
#### Options
You can set the injected library as the default library to use. 
```plang
- inject db, /npgsql/lib/net8.0/Npgsql.dll, set as default
```

#### Nuget locations
- [Postgresql](https://www.nuget.org/packages/Npgsql)
- [MySql/MariaDb](https://www.nuget.org/packages/MySqlConnector)
- Microsoft SQL Server - already available in plang

Any library that implements IDbConnection should work.

### settings Service

This allows you to change where the settings are stored. You need to implement the [ISettingsRepository](https://github.com/PLangHQ/plang/blob/main/PLang/Interfaces/ISettingsRepository.cs). 

This will allow you to create implementation to read .env files, Azure Vault, AWS Secret manager, Google Cloud Secret Manager or other storages for sensitive data.

```plang
- inject settings, /environment_vars/myimplementation.dll
```
### caching Service

Plang uses in memory caching. For services that need distributed caching, you can implement the abstract class [AppCache](https://github.com/PLangHQ/plang/blob/main/PLang/Interfaces/IAppCache.cs)

Check out the InMemory cache implementation that plang uses, [InMemoryCaching.cs](https://github.com/PLangHQ/plang/blob/main/PLang/Services/CachingService/InMemoryCaching.cs)

```plang
- inject caching, /mycaching/mycaching.dll
```

### llm Service

You can overwrite the LLM service that comes with plang. There is already [OpenAIService](https://github.com/PLangHQ/services/blob/main/OpenAiService/OpenAiService.cs) that you can use instead of the built in LLM plang service (Note: plang uses OpenAI but you dont need API key).

You need to implement the [ILlmService](https://github.com/PLangHQ/plang/blob/main/PLang/Interfaces/ILlmService.cs) interface

```plang
- inject llm, /myllm/myll.dll
```

If you want to build your code using your own LLM service, you MUST inject it like this in you Events.goal or Start.goal.

```plang
@llm=/myllm/myll.dll
```

### askuser Service

The plang language asks the user for information when it needs it. You need to implement the [IAskUserHandler](https://github.com/PLangHQ/plang/blob/main/PLang/Interfaces/IAskUserHandler.cs).

```plang
- inject askuser, /myAskUser/myAskUser.dll
```

You can check out [AskUserMessage](https://github.com/PLangHQ/services/tree/main/PLang.AskUserMessage), an implementation of using Message and plang code to implement a service. 

### encryption Service

The plang language uses AES256 for encryption. The private key is stored using the ISettingsRepository in the default implementation. 

You can change the implementation by implementing the [IEncryption](https://github.com/PLangHQ/plang/blob/main/PLang/Interfaces/IEncryptionService.cs) interface. This way you can choose your own encryption standard or your own storage medium.

```plang
- inject encryption, /myEncryption/myEncryption.dll
```

### archiver Service

Archiver service handles compression and decomression in the plang language. Plang language uses zip compression.

```plang
- inject archiver, /myarchiver/myarchiver.dll
```
Checkout the [plang implementation](https://github.com/PLangHQ/plang/blob/main/PLang/Services/ArchiveService/ZipArchive.cs)

### logger Service

You can overwrite the logger in the language by implementing the Microsoft.Extensions.Logging.ILogger interface.

```plang
- inject logger, /MyLogger/MyLogger.dll
```
Checkout the [plang implementation](https://github.com/PLangHQ/plang/blob/main/PLang/Services/LoggerService/Logger.cs)

## Implementing a Custom Service

Step-by-step:

- You need to have [Visual Studio Community](https://visualstudio.microsoft.com/vs/community/) installed (it's free). 
- [Download the PLang](https://github.com/PLangHQ/plang/tree/main) project
- Create a new project in Visual Studio, give it a name. For this example, `MyAmazingCompression`
- Add the PLang project as reference 
- Create your service.cs file, e.g. `MyAmazingCompression.cs`
- Implement the interface for the service you want to inject.

For example, creating a custom logger:

```csharp
using PLang.Interfaces;

public class MyAmazingCompression : IArchiver
{
    public async Task CompressDirectory(string sourceDirectoryName, string destinationArchiveFileName, int compressionLevel = 0, bool includeBaseDirectory = true)
	{		
        // Custom logic
    }
    public async Task CompressFile(string filePath, string saveToPath, int compressionLevel = 0)
    {
        // Custom logic
    }

    public async Task CompressFiles(string[] filePaths, string saveToPath, int compressionLevel = 0)
    {
        // Custom logic
    }

    public async Task DecompressFile(string sourceArchiveFileName, string destinationDirectoryName, bool overwrite = false)
    {
        // Custom logic
    }
}
```
Now compile the project, copy the `MyAmazingCompression.dll` into your `.services/MyAmazingCompression/` folder.

Now add to your Start.goal
```plang
Start
...
- inject archiver, MyAmazingCompression
...
```

## Using NuGet Packages in PLang

PLang supports the integration of NuGet libraries, especially those implementing service contracts like `IDbConnection`. To use a NuGet library in PLang:

1. **Download the Package**: Visit the NuGet library page and click "Download Package" to receive a `.nuget` file.
2. **Convert to Zip**: Rename the downloaded `.nuget` file to `.zip`.
3. **Unzip and Place in Services Folder**: Unzip the file and place its contents into the PLang `.services` folder.
4. **Reference in PLang**: Point to the unzipped library in your PLang `inject` step.

This process allows you to leverage the vast array of existing libraries in NuGet for enhancing your PLang applications.
