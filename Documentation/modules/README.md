# Writing plang code

This document serves as your gateway to programming in PLang, providing an overview of the built-in modules and their capabilities. 

Here, you'll find concise descriptions of each module's core functions along with practical examples to kickstart your programming tasks. Whether you're handling data, automating processes, securing information, or interacting with web services, these modules equip you with a broad range of tools to implement solutions efficiently. 

Explore the possibilities and leverage these examples as a foundation for your PLang projects.

You can also create your own [custom module](https://github.com/PLangHQ/modules)

## Table of Contents

1. [Http](#http) - Conduct web communications and handle HTTP requests seamlessly.
2. [File](#file) - Read, write, and manage files and directories with ease.
3. [Db](#db) - Interact with databases for comprehensive data manipulation.
4. [CallGoal](#callgoal-overview)  - Modularize your PLang scripts by invoking other goals.
19. [Llm](#llm) - Leverage language models for text analysis, generation, and translation.
15. [Output](#output) - Display information and interact with users through various output formats.
20. [LocalOrGlobalVariable](#localorglobalvariable) - Manage local and global variables within your PLang environment.
11. [Conditional](#conditional-overview) - Implement dynamic decision-making within your PLang scripts.
12. [Loop](#loop) - Iterate through collections and execute repetitive tasks.
5. [Compression](#compression-overview) - Optimize storage and manage data with compression techniques.
6. [Cryptographic](#cryptographic) - Protect your data with encryption, decryption, and hashing tools.
7. [Caching](#caching) - Enhance data retrieval efficiency with caching strategies.
8. [WebCrawler](#webcrawler) - Automate web browsing tasks for data interaction and extraction.
9. [Python](#python) - Seamlessly integrate and execute Python scripts within PLang.
10. [Schedule](#schedule) - Schedule tasks efficiently based on specific timing requirements.
13. [Webserver](#webserver) - Set up and manage web servers to handle web content and requests.
14. [Blockchain](#blockchain) - Interact with blockchain technologies for wallet and smart contract management.
16. [Terminal](#terminal) - Execute terminal commands directly from your PLang scripts.
17. [Message](#message) - Secure your communication channels within applications.
18. [CultureInfo](#cultureinfo) - Utilize cultural and regional settings for localized applications.
21. [Code](#code-overview) - Code generation

Each entry provides a glimpse into the module's primary functions, illustrating how you can incorporate these tools into your PLang projects. For detailed guidance and further exploration, the linked documentation offers comprehensive insights into each module's full capabilities.

Each section provides a snapshot of the module's capabilities, key actions, and quick start examples, guiding you through the initial steps to leverage these powerful features in your PLang projects. 

For more detailed information on each module, follow the provided documentation links.

# Blockchain

The Blockchain in Plang enables users to interact with various blockchain technologies, facilitating tasks like wallet management, message signing, smart contract interaction, and more.

## Key Actions

- **Wallet Information**: Retrieve wallet addresses and balances.
- **Message Signing**: Sign and verify messages to ensure authenticity.
- **Smart Contracts**: Interact with smart contracts, including querying balances and listening to events.
- **Transactions**: Send transactions and handle ether transfers with ease.

## Quick Start

### Get Wallet Address
```plang
- get current address, write to %address%
- write out %address%
```

### Sign and Verify a Message
```plang
- sign "Hello world", write to %signature%
- verify signature, "Hello world", %signature%, %address%, write to %result%
- write out "Is message verified: %result%"
```

### Interact with a Smart Contract
```plang
- set to mumbai testnet
- get balance on 0xContractAddress for address 0xYourAddress, write to %balance%
- write out 'Balance: %balance%'
```

### Transfer Ether
```plang
- transfer to 0xRecipientAddress, ether amount 0.1, gas price 50 gwei, write to %transactionHash%
- write out "Transaction hash: %transactionHash%"
```

[Link to docs](./PLang.Modules.BlockchainModule.md)
# Caching

Caching is a technique to store and retrieve data more efficiently by keeping a temporary copy of frequently accessed information, reducing the need to access slower storage layers.

## Key Actions

- **Set Cache**: Store data with optional expiration.
- **Get Cache**: Retrieve stored data.
- **Remove Cache**: Delete specific cached data.
- **Cache Expiration**: Manage data lifetime in the cache.

## Quick Start

### Set Cache with Expiration
```plang
- cache 'userData' for 30 minutes, to 'userKey'
```

### Get Cached Item
```plang
- get cache 'userKey', write to %cachedData%
```

### Remove Cached Item
```plang
- remove cache 'userKey'
```

For more detailed examples and advanced caching strategies, refer to the [Caching Documentation](./PLang.Modules.CachingModule.md).
# CallGoal Overview

The `CallGoal` in PLang allows for the invocation of other goals within a script, facilitating modular programming and task automation. It's essential for creating reusable and maintainable code.

## Key Actions

- **Invoke Goals**: Easily call other goals within your PLang script.
- **Parameter Passing**: Pass parameters to goals for dynamic execution.
- **Synchronous and Asynchronous Calls**: Choose to wait for a goal to complete or continue execution.
- **Result Handling**: Capture and utilize the output from called goals.

## Quick Start

### Calling a Simple Goal
Invoke a goal to perform a specific task without parameters.
```plang
- call !LogCurrentTime
```

### Passing Parameters to a Goal
Execute a goal with parameters to tailor its operation.
```plang
- call !ResizeImage imagePath="path/to/image.jpg", width=800, height=600
```

### Handling a Goal's Return Value
Use the result of a goal in subsequent steps.
```plang
- call !CalculateDiscount price=100, discountRate=20, write to %discountedPrice%
- write out "Discounted price: %discountedPrice%"
```

For a deeper dive into the `CallGoal`, including advanced scenarios and best practices, refer to the [detailed documentation](./PLang.Modules.CallGoalModule.md).
### PLang Code Overview

The Code in PLang simplifies code generation, manipulation, and common programming tasks, making it accessible for users of all skill levels.

#### Key Actions
- **Name Manipulation:** Extract, combine, or modify parts of names.
- **String Operations:** Convert case, generate random data, and format strings.
- **File Handling:** Read from, write to, and manipulate file paths and contents.
- **Task Execution:** Run tasks and process their outputs.

#### Quick Start

1. **Extracting Names**
   Extract first and last names from a full name.
   ```plang
   Code
   - set %fullName% as 'Jane Doe'
   - [code] extract first name from %fullName%, write to %firstName%
   - [code] extract last name from %fullName%, write to %lastName%
   ```

2. **Uppercase Conversion**
   Convert a string to uppercase.
   ```plang
   Code
   - set %text% as 'hello world'
   - [code] uppercase %text%, write to %upperText%
   ```

3. **Random Data Generation**
   Generate a list of random data.
   ```plang
   Code
   - [code] generate random data list with 5 items, write to %dataList%
   ```

4. **File Writing**
   Write a string to a file.
   ```plang
   Code
   - set %content% as 'Hello, PLang!'
   - [code] write %content% to file 'hello.txt'
   ```

5. **File Extension Removal**
   Remove the extension from a filename.
   ```plang
   Code
   - set %filename% as 'report.pdf'
   - [code] remove extension from %filename%, write to %nameWithoutExt%
   ```

For more detailed examples and documentation, refer to the [PLang Code Documentation](./PLang.Modules.CodeModule.md).
# Compression Overview

The Compression enables efficient file and directory compression and decompression, optimizing storage and data transfer.

### Key Actions
- **Compress Files/Directories**: Compress single or multiple files, or entire directories into ZIP archives.
- **Decompress Archives**: Extract files from ZIP archives, with options for specific extraction paths and overwriting existing files.
- **Adjust Compression Level**: Customize the compression level for balancing between speed and compression ratio.
- **Cleanup**: Remove temporary or no longer needed compressed files and directories.

### Quick Start

**Compress a Single File**
```plang
- compress `report.txt` to `report.zip`
```

**Compress a Directory**
```plang
- compress directory `logs` to `logs-archive.zip` with compression level 9, include base directory
```

**Decompress a File**
```plang
- uncompress `archive.zip` to `./extracted/`, overwrite
```

These examples demonstrate how to quickly get started with the Compression, covering basic compression and decompression tasks. For further details and advanced features, please refer to the [Compression documentation](./PLang.Modules.CompressionModule.md).
# Conditional Overview

The Conditional in PLang enables dynamic decision-making in your programs, allowing actions to be executed based on specific conditions.

## Key Actions

- **Variable Condition Checks**: Evaluate if variables meet certain conditions (true, false, empty, specific value).
- **File and Directory Checks**: Determine the existence of files or directories.
- **Comparison Checks**: Compare variables to values or other variables (greater than, less than, equal to).
- **Date Range Checks**: Verify if dates fall within or outside specific ranges.

## Quick Start

### Checking a Variable for True Value
```plang
- set var 'IsActive' as true
- if %IsActive% is true then
    - write out 'The feature is active'
```

### Checking for File Existence
```plang
- set var 'ConfigPath' as 'config/settings.json'
- if file at %ConfigPath% exists then
    - write out 'Configuration file found'
```

### Comparing Numeric Values
```plang
- set var 'Temperature' as 75
- if %Temperature% is greater than 70 then
    - write out 'It is warm outside'
```

These examples illustrate how to use the Conditional to make decisions based on variable states, file existence, and numeric comparisons, providing a foundation for building more complex logic in your PLang programs.

[Link to docs](./PLang.Modules.ConditionalModule.md)
# Cryptographic

The Cryptographic in PLang provides essential tools for data security, including encryption, decryption, hashing, and token management, ensuring secure data handling and authentication.

## Key Actions

- **Encryption & Decryption**: Secure sensitive information by converting it into a non-readable format and back.
- **Hashing**: Generate a fixed-size string from input data, crucial for storing passwords securely.
- **Token Management**: Create and validate bearer tokens for managing user sessions and access control.
- **HMAC SHA Hashing**: Use a secret key to hash data, ensuring both data integrity and authentication.

## Quick Start

### Encrypting and Decrypting Content
```plang
- set var %text% to 'Hello PLang world'
- encrypt %text%, write to %encryptedText%
- decrypt %encryptedText%, write to %decryptedText%
```

### Hashing Passwords
```plang
- set var %password% as 'MySuperPassword123.'
- create salt, write to %salt%
- hash %password% using salt %salt%, write to %hashedPassword%
```

### Generating and Validating Bearer Tokens
```plang
- set var %uniqueString% to 'user123'
- generate bearer token for %uniqueString%, expires in 2 weeks, write to %bearerToken%
- validate bearer token %bearerToken%, write to %isValidBearer%
```

For a deeper dive into the Cryptographic's capabilities and more examples, refer to the [full documentation](./PLang.Modules.CryptographicModule.md).

## CultureInfo

The `CultureInfo` in Plang allows for the manipulation and utilization of cultural and regional settings, affecting how dates, times, numbers, and strings are formatted and displayed.

**Key Actions**

- Setting the culture and UI culture to specific locales.
- Formatting dates, numbers, and currency based on the current culture.
- Parsing dates according to culture-specific formats.
- Comparing strings with culture-specific sorting rules.

**Quick Start**

To set the culture to US English and display a date in that culture's format:

```plang
CultureInfo
- set culture to en-US
- write out %date%
```

To format a number as currency in Japanese culture:

```plang
CultureInfo
- set culture to ja-JP
- write out "¥1,234"
```

To compare strings using Swedish sorting rules:

```plang
CultureInfo
- set culture to sv-SE
- if "äpple" comes before "banan" then write out "Correct order in Swedish."
```

[Link to docs](./PLang.Modules.CultureInfoModule.md)
# Db

The Db facilitates database interactions, enabling data manipulation and management within PLang code.

## Key Actions
- **Data Manipulation**: Perform CRUD operations (Create, Read, Update, Delete) on database records.
- **Schema Management**: Create, alter, and drop tables to define or modify the database structure.
- **Transaction Handling**: Execute multiple operations in a single, atomic transaction to maintain data integrity.
- **Advanced Features**: Utilize caching, retry logic, and unique indexing for optimized data operations.

## Quick Start
### Selecting Data
Retrieve all records from the `tasks` table.
```plang
- select * from tasks, write to %tasks%
- go through %tasks%, call !PrintOut
```
### Inserting Data
Add a new task with a description and due date.
```plang
- insert into tasks (description, due_date) values ('New Task', '2023-12-01'), write to %newTaskId%
```
### Updating Data
Update the description of a task identified by its ID.
```plang
- update tasks set description='Updated Task' where id=1
```
### Deleting Data
Remove a task from the `tasks` table by its ID.
```plang
- delete from tasks where id=1
```

[Link to docs](./PLang.Modules.DbModule.md)
# File

The File in Plang facilitates operations on files and directories, including reading, writing, and managing file systems.

## Key Actions

- **Reading and Writing**: Supports text, Excel, and CSV files.
- **File Management**: Copy, delete, and manage files and directories.
- **Monitoring**: Listen to file changes in real-time.
- **Data Processing**: Auxiliary functions for handling file data.

## Quick Start

### Read from a Text File
```plang
- read 'example.txt' into %content%
- write out %content%
```

### Write to a Text File
```plang
- write to 'example.txt', 'Hello, World!', overwrite if exists
```

### Copy and Delete a File
```plang
- copy 'source.txt' to 'destination.txt', overwrite if exists
- delete file 'source.txt'
```

For more detailed examples and functionalities, refer to the [File Documentation](./PLang.Modules.FileModule.md).
# Http

The Http enables web communication by supporting various HTTP requests, making it essential for interacting with web services and APIs.

## Key Actions

- **Performing HTTP Requests:** Supports GET, POST, PUT, DELETE, PATCH, HEAD, and OPTIONS requests.
- **Handling Data:** Allows sending and receiving data, including multipart/form-data for file uploads.
- **Customization:** Offers the ability to customize requests with headers and set request timeouts.

## Quick Start

### GET Request
Retrieve data from a web service.
```plang
Http
- GET https://httpbin.org/get, write to %getResponse%
```

### POST Request
Send data to a web service.
```plang
Http
- post https://httpbin.org/post
    data='{"key":"value"}'
    write to %postResponse%
```

### Custom Request with Headers
Send a custom request with additional headers.
```plang
Http
- request https://httpbin.org/anything
    method='GET'
    headers={'X-Custom-Header': 'Value'}
    write to %customResponse%
```

[Link to docs](./PLang.Modules.HttpModule.md)
### LlmModule

The LlmModule is a versatile tool for natural language processing tasks, including sentiment analysis, text categorization, content generation, and more.

#### Key Actions
- **Sentiment Analysis:** Determine the sentiment of a given text.
- **Text Categorization:** Classify text into predefined categories.
- **Content Generation:** Generate text based on a given prompt.
- **Question Answering:** Provide answers to user-posed questions.
- **Language Translation:** Translate text from one language to another.

#### Quick Start
To perform sentiment analysis:
```plang
LLM
- set %comment% as 'This is awesome'
- [llm] system: give me sentiment from the user comment
        user:  %comment%
        scheme: {sentiment:negative|neutral|positive}
        write to %result%
- write out 'The comment is: %result.sentiment%'
```
To generate content based on a prompt:
```plang
LLM
- set %prompt% as 'Write a short story about a space adventure'
- [llm] system: generate a story based on the prompt
        user: %prompt%
        model: 'gpt-4-turbo'
        temperature: 0.7
        maxLength: '2000'
        write to %story%
- write out 'Here is your story: %story%'
```
For more examples and detailed documentation, refer to the [LlmModule Documentation](./PLang.Modules.LlmModule.md).
### LocalOrGlobalVariable

This allows for the manipulation and monitoring of variables within PLang, supporting both local and static (global) scopes.

#### Key Actions

- **Variable Management:** Set, get, and remove variables.
- **Data Manipulation:** Append data to variables and convert variables to Base64.
- **Event Handling:** Execute actions on variable creation, change, or removal.

#### Quick Start

To set and retrieve a local variable:
```plang
- set var 'username' to 'JohnDoe'
- get var 'username', write to %userName%
- write out %userName%
```
To monitor variable changes and respond to events:
```plang
- when var 'theme' changes, call !ThemeChanged
```

[Link to docs](./PLang.Modules.LocalOrGlobalVariableModule.md)
### Loop

Loop through collections like lists and dictionaries, performing actions on each element.

#### Key Actions
- **Iterate Through Lists**: Loop through each item in a list, executing specified actions.
- **Iterate Through Dictionaries**: Go through each key-value pair in a dictionary, performing actions.
- **Parameterized Loops**: Pass additional parameters to loops for more complex operations.
- **Loop Termination**: Ensure loops terminate correctly to avoid infinite looping.
- **Comments in Loops**: Use comments within loops for better code clarity.

#### Quick Start
Loop through a list of products and display each product's name and price:
```plang
- add {"Name":"Product1", "Price":111} to list, write to %products%
- add {"Name":"Product2", "Price":222} to list, write to %products%
- go through %products% call !ShowProduct, item=%product%, list=%products%, key=1
```
ShowProduct Goal:
```plang
ShowProduct
- write out %product.Name% - %product.Price%
```

[Link to docs](./PLang.Modules.LoopModule.md)
### Message

The Message facilitates secure and private communication, leveraging encryption for user privacy.

#### Key Actions
- **Sending and Receiving Messages:** Securely send messages to others and receive messages.
- **Managing Accounts:** Switch between different messaging accounts.
- **Listening for Messages:** Set up listeners for new messages, including filtering by date or sender.
- **Message Expiration:** Send messages that auto-expire after a set period.

#### Quick Start
- **Retrieve Your Public Key:** Share your public key for private messaging.
  ```plang
  Message
  - get public key for messages, write to %pubKey%
  - write out %pubKey%
  ```
- **Send a Message to Another User:** Use their public key to send a secure message.
  ```plang
  Message
  - send message to %pubKey%, 'Hello, how are you?'
  ```
- **Listen for New Messages:** Automatically process new messages as they arrive.
  ```plang
  Message
  - listen for new message, call !NewMessage, write content to %messageContent%
  ```

[Link to docs](./PLang.Modules.MessageModule.md)
### Output

The Output in PLang is designed for displaying information and interacting with users in a console environment. It supports a variety of output formats and user inputs, making it a versatile tool for developers.

#### Key Actions

- **Displaying Messages**: Easily output text to the console, including dynamic content from variables.
- **User Input**: Prompt users for input, supporting both text and numeric data.
- **Formatted Output**: Use buffers and types to format messages, enhancing readability.
- **Conditional Output**: Display messages or request input based on specific conditions or types.

#### Quick Start

##### Display a Greeting
```plang
Output
- write out 'Welcome to PLang!'
```

#### Ask for User Name and Greet
```plang
Output
- ask 'What is your name?', write to %name%
- write out 'Hello, %name%!'
```

### Display a Formatted Message
```plang
Output
- write out 'Processing your request...', type 'info'
```

These examples demonstrate how to use the Output to interact with users and display information. For more detailed information and advanced features, refer to the full documentation.

[Link to docs](./PLang.Modules.OutputModule.md)
# Python

The Python in PLang allows seamless integration and execution of Python scripts within PLang projects, catering to a variety of use cases from simple executions to complex parameter handling.

### Key Actions
- **Execute Python Scripts**: Run scripts with or without parameters.
- **Parameter Handling**: Support for unnamed, named, and variable extraction.
- **Custom Python Paths**: Specify different Python interpreters.
- **Debugging Support**: Execute scripts in terminal mode for debugging.
- **Output Management**: Capture and differentiate between standard output and error streams.

### Quick Start
Execute a simple Python script:
```plang
Python
- call main.py, write to %output%
- write out 'Script output: %output%'
```
Pass parameters to a script:
```plang
Python
- call data_analysis.py, dataset.csv, 'max size 50mb', write to %analysis_results%
- write out 'Analysis Results: %analysis_results%'
```
Capture output and errors:
```plang
Python
- call might_fail.py, std out variable name 'stdout', std error variable name 'stderr', write to %script_status%
- write out 'Script output: %script_status[stdout]%'
- write out 'Script error (if any): %script_status[stderr]%'
```

[Link to docs](./PLang.Modules.PythonModule.md)

# Schedule

The Schedule in PLang facilitates the organization and execution of tasks based on time, supporting one-time, recurring, and delayed tasks with ease.

## Key Actions

- **Sleeping for Durations**: Pause execution for a specified duration.
- **Scheduling Recurring Tasks**: Execute tasks at regular intervals or specific times.
- **Delaying Tasks**: Schedule tasks to run after a delay.
- **Utilizing Cron Expressions**: Leverage cron expressions for complex scheduling needs.

## Quick Start

To get started with scheduling tasks in PLang, here are some popular actions:

- **Pause Execution**: `SleepShortDuration - sleep for 1 second`
- **Schedule a Recurring Task**: `ScheduleRecurringTask - every 1 minute, call !ItIsCalled`
- **Display Current Time**: `OutputCurrentTime - write out %Now%`
- **Schedule a Task for a Specific Time**: `ScheduleSpecificTime - at 2.1.2024 22:19:49, call !TaskAtSpecificTime`

[Link to docs](./PLang.Modules.ScheduleModule.md)
# WebCrawler

The WebCrawler in Plang enables users to automate web browser tasks, such as navigating pages, interacting with elements, and extracting data.

### Key Actions
- **Navigation:** Open web pages and navigate through them.
- **Interaction:** Click buttons, fill forms, and interact with web elements.
- **Data Extraction:** Retrieve and process data from web pages.
- **Conditionals:** Perform actions based on web content.
- **Utility:** Take screenshots, wait for elements, and manage browser sessions.

### Quick Start
Here's how to perform some popular actions with the Selenium in Plang:

**Navigate to a URL:**
```plang
NavigateToURL
- go to https://example.com, dont show browser
```

**Click on an Element:**
```plang
ClickElement
- click button #submit
```

**Input Text:**
```plang
InputText
- set input #username value as 'user'
- set input #password value as 'pass'
```

**Extract Text:**
```plang
ExtractText
- find .welcome-message, write to %message%
```

**Conditional Logic:**
```plang
IfCondition
- if %message% contains 'Welcome' then
    - write out 'Login Successful'
```

These examples provide a glimpse into automating web interactions using the Selenium in Plang. For more detailed information and advanced usage, refer to the full documentation.

[Link to docs](./PLang.Modules.SeleniumModule.md)
### Terminal

The Terminal facilitates direct interaction with the command line, enabling execution of external commands and scripts within Plang.

#### Key Actions
- **Running Commands**: Execute any command available in the system's terminal.
- **Handling Output**: Capture the output, error stream, and exit codes of commands.
- **User Interaction**: Read input directly from the user through the terminal.
- **Environment and Directory Control**: Specify working directories and environment variables for commands.

#### Quick Start
- **Execute a Simple Command**: 
  ```plang
  Terminal
  - run 'echo' with parameters 'Hello World', write to %output%
  - write out '%output%'
  ```
- **Capture Command Output and Error**:
  ```plang
  Terminal
  - run 'yourCommand' with parameters 'yourParams', output delta %output%, error stream delta %error%, write to %result%
  - write out 'Output: %output%\nError: %error%'
  ```
- **Read User Input**:
  ```plang
  Terminal
  - read 'Enter your choice: ', write to %userChoice%
  - write out 'You chose: %userChoice%'
  ```

[Link to docs](./PLang.Modules.TerminalModule.md)
# Webserver

Easily create and manage web servers with the Webserver in Plang, offering a straightforward way to serve web content and handle requests.

## Key Actions
- **Start a Webserver**: Launch a basic or customized web server.
- **Manage Cookies**: Set, get, and delete cookies.
- **Handle Headers**: Write to response headers and retrieve request headers.
- **Security**: Secure your webserver with signed requests.
- **User Data**: Obtain user IP addresses and manage session data.

## Quick Start

### Start a Basic Webserver
```plang
Webserver
- start webserver
- write out 'Webserver started on http://localhost:8080'
```

### Set and Get Cookies
```plang
SetCookie
- write cookie
    name 'sessionId'
    value 'abc123'
    expires in 1 week
- write out 'Session cookie set'
```

### Secure Your Webserver
```plang
WebserverWithSignedRequests
- start webserver
    signed request required true
- write out 'Webserver started with signed request verification'
```

For more detailed examples and advanced configurations, refer to the [full documentation](./PLang.Modules.WebserverModule.md).