# Handling 3rd Party Libraries in PLang

When working with PLang, you might encounter scenarios where your code depends on external libraries not bundled with the PLang compiler. This guide will help you safely download and integrate these libraries into your PLang environment.

## Understanding the Error

If the PLang compiler throws an error indicating a missing file, it's likely that your code requires an external library. For instance, if you encounter an error related to a file you don't recognize, you will need to source this file from a trusted repository.

## Safe Download Practices from NuGet

NuGet.org is a popular platform for .NET libraries which can also be used with PLang. However, downloading and using third-party libraries requires caution:

- **Search for the Library**: Go to [nuget.org](https://www.nuget.org) and enter the missing file or library name in the search bar.
- **Assess the Library**: Before downloading, check the library's usage statistics. Libraries with higher usage are typically more reliable. However, high usage does not guarantee absolute safety.
- **Download the Library**: Only download libraries from reputable sources and authors.

## Steps to Integrate a NuGet Package into PLang

1. **Download the Package**: Navigate to the appropriate NuGet library page and click "Download Package" to obtain a `.nuget` file.
2. **Convert to Zip**: Change the extension of the downloaded `.nuget` file to `.zip`.
3. **Extract and Deploy**: Unzip the file and place its contents into the PLang `.services` folder. Create the `.services` folder in your PLang directory if it does not exist.

This method ensures that your PLang applications can utilize a wide range of functionalities provided by external libraries.

## Practical Example: Integrating the Markdig Library

Consider a scenario where you need to use the Markdig library to format text in Markdown. Here’s how you might encounter and resolve an error:

### PLang Code Sample

```plang
Run
- [code] generate list of 100 users,
    create comment text, convert to .md format, using 3rd party library(Markdig)
     with columns: name, email, comment, write to %users%
```

### Error and Resolution

Running the above script without the Markdig library installed will result in an error. Follow these steps to resolve it:

1. **Identify the Error**: The PLang compiler will indicate a missing Markdig file.
2. **Search on NuGet**: Find [Markdig on NuGet](https://www.nuget.org/packages?q=Markdig) and verify the library's activity and usage.
3. **Download and Install**: Follow the steps outlined above to download, convert, and deploy the Markdig library into your `.services` folder.

By following these guidelines, you can enhance your PLang applications while ensuring your system's security and integrity.