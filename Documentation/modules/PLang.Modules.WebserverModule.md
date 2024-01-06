
# Webserver

## Introduction
The Webserver module in the plang programming language is a powerful tool that allows you to create and manage a web server directly within your plang code. This module is designed to be simple to use, making it accessible for beginners, yet robust enough for more advanced users who need to perform complex web server tasks.

## For Beginners
A web server is a software that uses HTTP (Hypertext Transfer Protocol) to serve users' requests to view web pages on the internet. It delivers content, such as HTML pages, images, and other types of media, to a client – usually a web browser. The Webserver module in plang allows you to start a web server, handle web requests, and respond to them programmatically.

As a beginner, you might not be familiar with all the technical terms, but that's okay. Think of the Webserver module as a friendly waiter who takes your order (a web request) and brings you what you asked for (web content). With plang, you can tell this waiter what to serve and how to handle special requests.

## Best Practices for Webserver
When using the Webserver module in plang, it's important to follow best practices to ensure your web server runs smoothly and securely. Here's an example of a best practice:

Always use a proxy server that can provide SSL (Secure Sockets Layer) encryption when dealing with sensitive data, as the Webserver module does not support SSL directly. SSL helps protect information as it travels across the internet.

Here's a simple plang code example that demonstrates starting a web server and a note to use a proxy for SSL:

```plang
Webserver
- start webserver
- write out 'Webserver started on http://localhost:8080'
- write out 'Note: Use a proxy server to enable SSL for secure communication.'
```

## Examples

# Webserver Module Examples

The following examples demonstrate how to use the Webserver module in the plang language. These examples are sorted by their expected popularity based on common web server tasks.

## Start a Basic Webserver

```plang
Webserver
- start webserver
- write out 'Webserver started on http://localhost:8080'
```

## Start a Webserver with Custom Options

```plang
WebserverMoreOptions
- set var %host% as '127.0.0.1'
- set var %port% as 7070
- start webserver
    host %host%
    port %port%, max upload size 4mb, default response content encoding utf-8
    public paths: ['api', 'api.goal']
- write out 'Webserver started on http://%host%:%port%'
```

## Write to Response Header

```plang
SetResponseHeader
- write to response header
    name 'Content-Type'
    value 'application/json'
- write out 'Response header set for JSON content'
```

## Set a Cookie

```plang
SetCookie
- write cookie
    name 'sessionId'
    value 'abc123'
    expires in 1 week
- write out 'Session cookie set'
```

## Delete a Cookie

```plang
DeleteCookie
- delete cookie
    name 'sessionId'
- write out 'Session cookie deleted'
```

## Get User IP Address

```plang
GetUserIP
- get user ip
    header key 'X-Forwarded-For', write to %userIp%
- write out 'User IP: %userIp%'
```

## Get a Specific Request Header

```plang
GetRequestHeader
- get request header
    key 'User-Agent', write to %userAgent%
- write out 'User Agent: %userAgent%'
```

## Get a Cookie Value

```plang
GetCookieValue
- get cookie
    key 'sessionId', write to %sessionId%
- write out 'Session ID from cookie: %sessionId%'
```

## Start a Webserver with Signed Requests

```plang
WebserverWithSignedRequests
- start webserver
    signed request required true
- write out 'Webserver started with signed request verification'
```

Note: When creating examples, ensure that the natural language used is clear and maps correctly to the corresponding method in the `WebserverModule` class. Variables are used to capture return values and can be referenced in subsequent steps for demonstration purposes.


For a full list of examples, visit the plang Webserver module examples on GitHub: [Webserver Module Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Webserver)

## Step Options
When writing your plang code, you can enhance the functionality of each step by using the following options. Click on the links for more detailed information on how to use each one:

- [CacheHandler](/modules/cacheHandler.md)
- [ErrorHandler](/modules/ErrorHandler.md)
- [RetryHandler](/modules/RetryHandler.md)
- [CancellationHandler](/modules/CancelationHandler.md)
- [Run and Forget](/modules/RunAndForget.md)

## Advanced
For those who are ready to dive deeper into the Webserver module and understand how it maps to underlying C# implementations, please refer to the advanced documentation: [Advanced Webserver Module Information](./PLang.Modules.WebserverModule_advanced.md).

## Created
This documentation was created on 2024-01-02T22:35:01.
