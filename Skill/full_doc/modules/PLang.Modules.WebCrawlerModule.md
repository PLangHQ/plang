# WebCrawler

## Introduction
The WebCrawler module in the plang programming language provides a powerful and intuitive way to automate interactions with web pages. It allows developers to programmatically navigate websites, extract data, and perform actions like clicking buttons or filling out forms.

## For Beginners
WebCrawler is a tool that simulates human interaction with a website. Imagine you need to gather information from a website daily, such as checking the weather, prices of products, or updates on news articles. Instead of doing this manually, WebCrawler can automate the process. It can open a web browser, go to specific pages, and even log into accounts to access specific information. This automation is particularly useful for repetitive tasks and can save a significant amount of time.

## Best Practices for WebCrawler
When using the WebCrawler module in plang, it's important to follow certain best practices to ensure efficient and reliable scripts:

1. **Use clear and descriptive goal names**: Since each file can contain multiple goals, using descriptive names helps in understanding the purpose of each goal at a glance.
2. **Manage browser resources**: Always ensure to close the browser with the 'Close browser' step after your tasks are completed to free up system resources.
3. **Handle errors gracefully**: Use error handling steps to manage and respond to errors during web navigation or interaction.
4. **Avoid hardcoding sensitive information**: Use variables for sensitive data like usernames and passwords, and manage these securely.

### Example
Here's a simple example demonstrating some best practices:
```plang
WebCrawler
- start browser Chrome, dont show the browser
- navigate to URL 'https://example.com/login'
- input 'username' into '#username'
- input 'password123' into '#password'
- click '#submit-button'
- if %loginSuccess% then call LoginSuccessfull, else LoginFailed
- close browser

LoginSuccessfull    
- write out 'Login successful'

LoginFailed
- write out 'Login failed'

```
This script logs into a website and provides feedback based on whether the login was successful.

## Examples
Explore how to use the WebCrawler module with practical examples. These examples demonstrate various tasks like navigating web pages, extracting data, and more.

# WebCrawler Module Examples

## Start and Navigate
```plang
WebCrawler
- Start browser Chrome, headless mode
- Navigate to URL 'https://example.com'
```

## Click and Input
```plang
WebCrawler
- Navigate to URL 'https://example.com/login'
- Input 'username' into '#username'
- Input 'password123' into '#password'
- Click '#submit-button'
```

## Extract Content and Use
```plang
WebCrawler
- Navigate to URL 'https://example.com/data'
- Extract content from '.data-row', clear html, write to %dataRows%
- go through %dataRows%, call !ProcessDataRow
```

## Process Data Example
```plang
ProcessDataRow
- write out 'Data: %item%\n-------'
```

## Wait and Scroll
```plang
WebCrawler
- Navigate to URL 'https://example.com/large-page'
- Wait for 2 seconds
- Scroll to bottom
```

## Advanced Interaction
```plang
WebCrawler
- Start browser Chrome, headless mode, use user session from 'path/to/session'
- Navigate to URL 'https://example.com/settings'
- Select by text 'English' from '#language-dropdown'
- Submit '#settings-form'
```

## Take Screenshot
```plang
WebCrawler
- Navigate to URL 'https://example.com'
- Take screenshot of website, save to 'path/to/screenshot.png'
```

## Close Browser
```plang
WebCrawler
- Close browser
```

## Switch Tab and Extract
```plang
WebCrawler
- Start browser Chrome
- Navigate to URL 'https://example.com'
- Open new tab and navigate to 'https://example.com/second-page'
- Switch tab 0
- Extract content from '#main-content', write to %mainContent%
- write out %mainContent%
```

## Wait for Element and Focus
```plang
WebCrawler
- Navigate to URL 'https://example.com'
- Wait for element to appear '#input-field', timeout in 30 seconds
- Set focus on '#input-field'
- Input 'Hello World!' into '#input-field'
```

These examples cover a range of common tasks that can be performed using the WebCrawler module, demonstrating how to interact with web pages programmatically for tasks like navigation, data extraction, and user interaction simulation.

For a full list of examples, visit [WebCrawler Examples](https://github.com/PLangHQ/plang/tree/main/Tests/WebCrawler).

## Step Options
Each step in a WebCrawler script can be enhanced with specific handlers to manage caching, errors, retries, or cancellations. Here are some resources to help you implement these options:

- [CacheHandler](/CachingHandler.md)
- [ErrorHandler](/ErrorHandler.md)

- [CancellationHandler](/modules/CancelationHandler.md)
- [Run and Forget](/modules/RunAndForget.md)

## Advanced
For developers interested in the deeper technical aspects of the WebCrawler module, such as its integration with C#, refer to the advanced documentation [here](./PLang.Modules.WebCrawlerModule_advanced.md).

## Created
This documentation was created on 2024-07-26T10:53:04.