
# Http Module in PLang

## Introduction
The Http module in PLang is a powerful tool that allows you to perform various HTTP requests within your PLang scripts. This module is essential for interacting with web services, APIs, and other internet resources directly from your PLang code.

## For Beginners
HTTP, or Hypertext Transfer Protocol, is the foundation of data communication on the web. It's a protocol used by the internet to exchange information between a client (like your web browser) and a server (where websites are hosted). When you visit a website, send a form, or interact with web services, HTTP requests are what make it possible for information to be sent and received.

In PLang, the Http module simplifies the process of making these requests. You don't need to understand all the technical details of HTTP to use it. Think of it as a way to ask the internet to do something for you, like retrieving a webpage, sending data to a server, or asking a service for some information.

## Best Practices for Http
When using the Http module in PLang, it's important to follow best practices to ensure your code is efficient, readable, and error-resistant. Here are some tips:

- Use meaningful variable names to store responses, making your code easier to understand.
- Handle errors gracefully using the ErrorHandler module to avoid crashes or unexpected behavior.
- Use the CacheHandler to save responses and reduce the number of requests to a server.
- Implement the RetryHandler to automatically retry failed requests, which is useful for dealing with network instability.
- For non-critical requests, consider using the Run and forget module to execute them without waiting for a response.

Let's look at an example of a simple GET request with error handling:

```plang
Http
- GET https://httpbin.org/get, write to %getResponse%
- if %getResponse.status% != 200 then call !HandleError
- write out 'Status: %getResponse.status%, UserAgent: %getResponse.headers.User-Agent%, ip: %getResponse.origin%'
```

In this example, we make a GET request to `httpbin.org/get` and store the response in `%getResponse%`. We then check if the status code is not 200 (OK) and call a custom error handler if there's an issue. Finally, we output some details from the response.


# HttpModule Examples

The following examples demonstrate how to use the HttpModule in PLang to perform various HTTP requests. The examples are sorted by the most commonly used methods.

## GET Request

Retrieve data from a specified resource.

```plang
Http
- GET https://httpbin.org/get, write to %getResponse%
- write out 'UserAgent: %getResponse.headers.User-Agent%, ip: %getResponse.origin%'
```

## POST Request

Submit data to be processed to a specified resource.

```plang
Http
- post https://httpbin.org/post
    data='{"key1":"value1", "key2":"value2"}'
    signRequest
    write to %postResponse%
- write out %postResponse.tojson()%
```

## POST Multipart/Form-Data Request

Submit form data as multipart/form-data.

```plang
Http
- post multipart https://httpbin.org/post
    data: @file='1px.png', name='1px'
    write to %postResponse2%
- write out %postResponse2.tojson()%
```

## PUT Request

Replace all current representations of the target resource with the uploaded content.

```plang
Http
- put https://httpbin.org/put
    data='{"newKey":"newValue"}'
    write to %putResponse%
- write out %putResponse.tojson()%
```

## DELETE Request

Remove all current representations of the target resource given by a URI.

```plang
Http
- delete https://httpbin.org/delete, write to %delResponse%
- write out %delResponse.ToJson()%
```

## PATCH Request

Apply partial modifications to a resource.

```plang
Http
- patch https://httpbin.org/patch
    data='{"patchKey":"patchValue"}'
    write to %patchResponse%
- write out %patchResponse.ToJson()%
```

## HEAD Request

Asks for a response identical to a GET request, but without the response body.

```plang
Http
- head https://httpbin.org/get, write to %headResponse%
- write out 'Headers received: %headResponse.headers%'
```

## OPTIONS Request

Describe the communication options for the target resource.

```plang
Http
- option https://httpbin.org
    write to %optionsResponse%
- write out 'Allowed Methods: %optionsResponse.headers.Allow%'
```

## Custom Request with Headers and Timeout

Make a custom HTTP request with additional headers and a specified timeout.

```plang
Http
- request https://httpbin.org/anything
    method='CUSTOM'
    headers={'X-Custom-Header': 'Value', 'Accept': 'application/json'}
    timeout='30 seconds'
    write to %customResponse%
- write out %customResponse.tojson()%
```

Note: In the examples above, `%variableName%` is used to store the response from the HTTP request, which can then be used in subsequent steps for further processing or output.


For a full list of examples, visit the [PLang Http Module Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Http).

## Step Options
When writing your Http steps in PLang, you have several options to enhance functionality:

- [CacheHandler](/modules/CacheHandler.md) - Caches responses to avoid repeated requests.
- [ErrorHandler](/modules/ErrorHandler.md) - Manages errors that occur during HTTP requests.
- [RetryHandler](/modules/RetryHandler.md) - Automatically retries failed requests.
- [CancellationHandler](/modules/CancellationHandler.md) - Allows for the cancellation of ongoing requests.
- [Run and forget](/modules/RunAndForget.md) - Executes a request without waiting for the response, useful for background tasks.

Click the links for more details on how to use each option.

## Advanced
For those who want to dive deeper into the Http module and understand how it maps to underlying C# functionality, check out the [advanced documentation](./PLang.Modules.HttpModule_advanced.md).

## Created
This documentation was created on 2024-01-02T21:54:21.
