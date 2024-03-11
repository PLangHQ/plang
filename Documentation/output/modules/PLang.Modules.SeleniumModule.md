
# Selenium
## Introduction
Selenium is a powerful tool for automating web browsers, allowing you to perform tasks such as testing web applications, scraping web content, and automating repetitive web interactions. In the context of the plang programming language, Selenium is used to define goals and steps that interact with web pages in a structured and automated manner.

## For Beginners
If you're new to programming or technical concepts, think of Selenium as a robot that can use a web browser just like you do. It can go to websites, click on things, fill out forms, and even read the text on the page. The difference is that Selenium follows precise instructions you give it through the plang language, which means it can do these tasks much faster and more consistently than a human.

## Best Practices for Selenium
When writing plang code for Selenium, it's important to follow best practices to ensure your code is efficient, readable, and maintainable:

1. **Keep It Simple**: Start with simple goals and steps. Complex tasks can often be broken down into simpler ones.
2. **Use Comments**: Use comments to explain what each step is supposed to do. This helps others understand your code and can be a reminder for you later on.
3. **Handle Errors**: Anticipate and handle potential errors. Use the ErrorHandler module to manage unexpected issues gracefully.
4. **Avoid Hardcoding**: Use variables instead of hardcoding values. This makes it easier to update your code if something changes on the web page.
5. **Test Thoroughly**: Test each step individually and then as part of the whole goal to ensure everything works as expected.

### Example
Let's say you want to log into a website:

```plang
Login
- go to https://example.com/login, dont show browser
- set input #username value as 'myUsername'
- set input #password value as 'myPassword'
- click button #submit
- find text 'Welcome back!', write to %loginSuccess%
- if %loginSuccess% then
    - write out 'Login successful!'
```

In this example, we navigate to the login page, fill in the username and password, submit the form, and check for a welcome message to confirm the login was successful.

## Examples

# Selenium Module Examples

The Selenium module allows you to automate web browser interactions such as navigating to URLs, clicking on elements, inputting text, and extracting content. Below are examples of how to use the Selenium module in the plang language, sorted by the most commonly used methods.

## 1. Navigate to a URL

```plang
Selenium
- go to https://quotes.toscrape.com/, dont show browser
```

This example demonstrates how to navigate to a website without showing the browser window (headless mode).

## 2. Click on an Element

```plang
Selenium
- click href=/login
```

This step simulates a click on the login link on the page.

## 3. Input Text

```plang
Selenium
- [Selenium] set #username as 'test'
- set input #password value as '123'
```

These steps fill in the username and password fields with the provided credentials.

## 4. Submit a Form

```plang
Selenium
- submit form
```

This step submits the form on the current page.

## 5. Extract Content and Conditional Logic

```plang
Selenium
- find href="/logout", write to %isLoggedIn%
- if %isLoggedIn[1]% = 'Logout' then
    - write out 'Yes, I am logged in'
```

This example checks if the logout link is present and outputs a confirmation message if the user is logged in.

## 6. Click on the First Element with a Specific Class

```plang
Selenium
- click first .tag-item link
```

This step clicks on the first link with the class `tag-item`.

## 7. Extract Content from Multiple Elements

```plang
Selenium
- [Selenium] extract all .quote, clear html, write into %quotes%
- go through %quotes%, call !ShowQuote
```

This example extracts all elements with the class `quote`, clears the HTML formatting, and stores the results in a variable. It then iterates through the quotes and calls a custom goal for each one.

## 8. Custom Goal Example

```plang
ShowQuote
- write out 'Quote: c:\Users\Ingi Gauti\source\repos\plang\Tests\Selenium\Selenium.goal\n-------'
```

This custom goal outputs a formatted string to display a quote.

## Additional Examples

### Close Browser

```plang
Selenium
- close browser
```

Closes the browser instance.

### Take Screenshot

```plang
Selenium
- take screenshot, save to 'C:\Screenshots\page.png'
```

Takes a screenshot of the current browser view and saves it to the specified path.

### Wait for a Specific Duration

```plang
Selenium
- wait for 5 seconds
```

Pauses the execution for a specified duration, in this case, 5 seconds.

### Switch Browser Tab

```plang
Selenium
- switch to tab 2
```

Switches to the second tab in the browser window.

Remember to replace the placeholders with actual values relevant to your use case. The `%variableName%` syntax is used to store and reference values between steps.


For a full list of examples, visit the [plang Selenium examples repository](https://github.com/PLangHQ/plang/tree/main/Tests/Selenium).

## Step Options
Each step in your Selenium goal can be enhanced with additional options for better control and reliability. Click the links below for more details on how to use each option:

- [CacheHandler](/modules/handlers/CachingHandler.md)
- [ErrorHandler](/modules/handlers/ErrorHandler.md)
- [RetryHandler](/modules/handlers/RetryHandler.md)



## Advanced
For those who want to delve deeper into the capabilities of Selenium within plang, check out the [advanced documentation](./PLang.Modules.SeleniumModule_advanced.md). This section covers the underlying mapping with C# and provides insights into more complex scenarios.

## Created
This documentation was created on 2024-01-02T22:25:52.
