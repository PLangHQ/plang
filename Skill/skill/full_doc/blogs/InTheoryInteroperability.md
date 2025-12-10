# In Theory: Interoperability

"Interoperability" sounds like a big word, but it's really just a fancy way of saying you can move data from one system to another, without a ton of hassle.

## The Challenge of Moving Data

Back in the early days of software, getting data from one system into another was next to impossible. Each app had its own format, and if you wanted to pull out information, you'd need to know the ins and outs of that system—something only the developers or a few elite hackers could figure out.

## The Progress We've Made

Fast forward to today, and things have gotten a lot better. We now have standards like JSON, REST APIs, XML, and SQL databases that make it easier to transfer data between systems. These technologies give us the raw data we need to get info from one platform and put it into another.

But here’s the catch: this only works if you can actually access the raw data. Even now, you still need someone with technical expertise to pull the data out of one system and load it into another. It’s not like these systems are just going to talk to each other by themselves.

## Data Behind Closed Doors

Things get a lot trickier when the data you need is locked behind some kind of barrier—like inside a desktop application or hidden away on a website. In those cases, extracting the data can be really hard. So, what do we do? We use people. Humans have to open the website, search for the data, and manually copy it over.

## The Data Locked in Our Heads

And then there’s the trickiest type of data: what’s locked inside our own brains. People carry knowledge around with them that’s essential for decision-making, but trying to get that knowledge out is tough. Only recently has technology advanced enough to listen to someone speaking into a mic and transcribe their words. 

## An Impossible Problem?

I came across this quote in [an article on automation](https://hardcoresoftware.learningbyshipping.com/p/222-automating-processes-with-software) that stuck with me:

> "No amount of software is going to get you out of there because it is piecing together a bunch of inputs and outputs that are outside the bounds of a system."

The part about "outside the bounds of a system" really hit home. It’s true—no matter how good your software is, there’s always going to be some website or desktop app with data hidden away, or a phone call from a customer that throws everything off. You just can’t build software to handle every single piece of data, everywhere it exists. It seems impossible.

## Or Is It?

Let’s explore this idea with a bit of code in Plang. Imagine a scenario where:

1. You get info from a customer by voice.
2. You pull their data from an API.
3. You log into a government website to fetch more details.
4. You open a desktop app to grab additional info.
5. Then you combine all this into a single response for the user.

Here’s how you might do that in Plang:

```plang
Start
- use 'input' as audio
- use 'output' as audio
- ask "Please tell me your SSN", write to %audio%
- convert %audio% to text, write to %user%
- [llm] system: analyze the input, figure out his SSN
        user: %user%
        scheme: {ssn:string}
- call goal GetUserInfo
- call goal BrowseGovernment
- call goal OpenDesktopApp
- call goal StoreInfo
- call RespondToUser

GetUserInfo
- get http://myinternal.webservice/?ssn=%ssn%, write to %userInfo%

BrowseGovernment
- go to https://goverment.org
- set #username = %Settings.Username%
- set #password = %Settings.Password%
- click #login
- go to https://goverment.org/user/%ssn%
- extract #medicalInfo into %medicalInfo%
- extract #visionInfo into %visionInfo%

OpenDesktopApp
- run myapp.exe, maximize and focus
- click on 1200, 300 / put focus on input box
- type in %ssn%
- click on 1200, 500 / click the search button
- take screenshot, save to file/screenshot.jpg
- [llm] system: analyze the screenshot and give me the text
        user: file/screenshot.jpg
        scheme: { userGlasses: string, userStrength: string }

StoreInfo
- insert into user, %ssn%, %userInfo%, %medicalInfo%, %visionInfo%, %userGlasses%, %userStrength%

RespondToUser
- [llm] system: create a response to user from data
        user: %userInfo%, %medicalInfo%, %visionInfo%, %userGlasses%, %userStrength%
        write to %response%
- convert %response% to audio, write to %audioResponse%
- write out %audioResponse%
```

In just over 40 lines of code, we’ve pulled data from four very different sources: a voice input, an API, a website, and a desktop application. We’ve brought all that data together in one place to give a single response to the user.

Now, let’s dive into what each piece of the code does next…
## Let's Go Over the Code

Let's break down what each part of the Plang code does:

```plang
- use 'input' as audio, from default mic
```
We start by changing the default input. Normally, Plang takes input from the terminal, but here, we're telling it to use the microphone for audio input. This allows us to capture what the user says.

```plang
- use 'output' as audio
```
Similarly, we're switching the output to audio. Instead of outputting text to a screen, the program will convert text into an audio stream, which will be played back to the user.

```plang
- ask "Please tell me your SSN", write to %audio%
```
This line sends a spoken request to the user, asking for their SSN. The user's response is captured as audio and stored in the `%audio%` variable.

```plang
- convert %audio% to text, write to %user%
```
Once we have the user's spoken response, we convert the audio to text and store it in the `%user%` variable so that we can process it.

```plang
- [llm] system: analyze the input, figure out his SSN
        user: %user%
        scheme: {ssn:string}
```
Here, we ask a large language model (LLM) to analyze the user’s text input to extract the SSN. The result is stored in the `%ssn%` variable.

> **Note:** This is written as if everything goes perfectly. In reality, you'd need error handling for cases where the user gives invalid input or doesn't follow the expected format.

```plang
- call goal GetUserInfo
- call goal BrowseGovernment
- call goal OpenDesktopApp
- call goal StoreInfo
- call RespondToUser
```
We then call a series of predefined "goals" that perform various tasks:
- `GetUserInfo` fetches customer data from an internal REST API.
- `BrowseGovernment` logs into a government website to retrieve data.
- `OpenDesktopApp` interacts with a desktop app to pull more info.
- `StoreInfo` saves all the collected data.
- `RespondToUser` gives a response back to the user.

### GetUserInfo

This is a straightforward REST API call:

```plang
- get http://myinternal.webservice/?ssn=%ssn%, write to %userInfo%
```
We query an internal web service using the SSN as the parameter and store the returned user information in the `%userInfo%` variable.

### BrowseGovernment

This section automates logging into a government website and pulling specific data:

```plang
- go to https://goverment.org
- set #username = %Settings.Username%
- set #password = %Settings.Password%
- click #login
- go to https://goverment.org/user/%ssn%
- extract #medicalInfo into %medicalInfo%
- extract #visionInfo into %visionInfo%
```
We navigate to the government website, input login credentials stored in the `Settings`, and then log in. Once logged in, we navigate to a page containing the user’s info based on their SSN. We then extract the medical and vision data, saving them into `%medicalInfo%` and `%visionInfo%`.

### OpenDesktopApp

Interacting with desktop apps is a bit trickier, so we simulate mouse movements and clicks by using pixel coordinates:

```plang
- run myapp.exe, maximize and focus
- click on 1200, 300 / put focus on input box
- type in %ssn%
- click on 1200, 500 / click the search button
- take screenshot, save to file/screenshot.jpg
- [llm] system: analyze the screenshot and give me the text
        user: file/screenshot.jpg
        scheme: { userGlasses: string, userStrength: string }
```
We launch the desktop app `myapp.exe`, maximize the window, and focus on it. Then we simulate clicking at specific coordinates to place the cursor in the input box and enter the SSN. After clicking "Search," we take a screenshot of the results.

Since we can’t directly extract the data from a desktop app, we upload the screenshot to the LLM, which performs optical character recognition (OCR) to analyze the text in the image. This gives us the variables `%userGlasses%` and `%userStrength%`, which hold the extracted data.

### StoreInfo

Now that we've gathered all the information, we store it in a database:

```plang
StoreInfo
- insert into user, %ssn%, %userInfo%, %medicalInfo%, %visionInfo%, %userGlasses%, %userStrength%
```
Here, we insert the SSN, user info, medical data, vision data, and any additional information (like the user's glasses prescription and strength) into the user database.

### RespondToUser

Finally, we respond to the user by creating a custom response based on the data we’ve collected:

```plang
RespondToUser
- [llm] system: create a response to user from data
        user: %userInfo%, %medicalInfo%, %visionInfo%, %userGlasses%, %userStrength%
        write to %response%
- convert %response% to audio, write to %audioResponse%
- write out %audioResponse%
```
We ask the LLM to generate a response that summarizes all the information we've gathered. The LLM crafts the message using the `%userInfo%`, `%medicalInfo%`, `%visionInfo%`, and other data. Once the response is created, we convert it to audio and play it back to the user.


## Conclusion
This entire flow—from gathering audio input to pulling data from various sources and returning a spoken response—happens in around 40 lines of code, demonstrating how powerful interoperability using plang can be.

The title of this article is "In Theory: Interoperability" because, in reality, you can't do all this in Plang today. 

What’s missing is the ability to listen to a user, convert their audio to text, and automate desktop interaction. These pieces just need to be built—there are no real technical barriers. Once those modules are in place, the code you see above should work just fine.

You then of course have all the exceptions to handle, errors, when do you know when user has stopped speaking and so on. 


## More Information

Interested in learning more about Plang? Here are some useful resources to get started:

- [Basic Concepts and Lessons](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Lesson%202.md)
- [Todo Example](https://github.com/PLangHQ/plang/blob/main/Documentation/Todo_webservice.md) to build a simple web service
- Explore the [GitHub repo](https://github.com/PLangHQ/) for source code
- Join our [Discord Community](https://discord.gg/A8kYUymsDD) for discussions and support
- Or chat with the [Plang Assistant](https://chatgpt.com/g/g-Av6oopRtu-plang-help-code-generator) to get help with code generation