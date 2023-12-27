### Services in PLang

#### Skill Level: Advanced

Services in PLang are powerful tools that extend its functionality through dependency injection. Developers can customize PLang by injecting various service types, enhancing its capabilities to suit specific needs.

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
Services must be located under the `services` folder in PLang.

#### Detailed Service Implementation

##### db Service
```plang
- inject db, /services/npgsql/lib/net7.0/Npgsql.dll, global
```

##### settings Service
```plang
- inject settings, /environment_vars/myimplementation.dll
```

##### llm Service
```plang
- inject llm, /myllm/myll.dll
```
or 
```plang
@llm=/myllm/myll.dll
```

##### askuser Service
```plang
- inject askuser, /myAskUser/myAskUser.dll
```

##### encryption Service
```plang
- inject encryption, /myEncryption/myEncryption.dll
```

##### archiver Service
```plang
- inject archiver, /myarchiver/myarchiver.dll
```

### Implementing a Custom Service
A step-by-step guide to creating a custom service. For example, creating a custom logger:

```csharp
public class CustomLogger : ILogger
{
    public void Log(string message)
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
