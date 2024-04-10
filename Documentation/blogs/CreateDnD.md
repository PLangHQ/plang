# Dive Into Building a D&D Game Assistant with Plang

This tutorial demonstrates the ability of the [Plang programming language](https://plang.is).

In this tutorial, we're fast-tracking the creation of a Dungeons & Dragons (D&D) Game Assistant using Plang, a language that merges natural language with programming. This tool is inspired by the D&D applications on ChatGPT but aims to localize this experience with a flair for customization. 

### TL;DR

Don't want to read, just want to play, here you go

1. Ensure [Plang is installed](https://github.com/PLangHQ/plang/blob/main/Documentation/GetStarted.md) on your system.
2. Choose your version:
   - For simplicity: Download [DnD.zip](https://github.com/PLangHQ/apps/tree/main/DnD/DnD.zip). (simpler, support plang project but more expensive)
   - If you have OpenAI Key: Get [DnD_OpenAI.zip](https://github.com/PLangHQ/apps/tree/main/DnD/DnD_OpenAI.zip). (complexer registration, cheaper)
3. Unzip the file
4. Navigate to the folder you unzipped to and run this command
    ```bash
    plang
    ```
5. Have fun

## Lets Code DnD in Plang

### Quick Setup Guide
Before jumping in, ensure you have Plang installed and if you want to develop the app more, get the IDE ready for a smoother experience. While D&D knowledge isn't mandatory, it'll enrich your journey. 

Check out the [Plang Getting Started guide](https://github.com/PLangHQ/plang/blob/main/Documentation/GetStarted.md) for Plang setup details. You need it before programming in Plang.

### Our Creation
We'll build our assistant in pieces, focusing on:
1. **Setup**: Crafting the database to hold game and storyline data.
2. **Start**: The launchpad for new games or continuing sagas.
3. **CreateNewGame**: Setting the scene with game environments and characters.

Let's gear up and dive into coding!

### Initializing Your D&D Game Assistant: `Setup.goal`

To kickstart our D&D Game Assistant, we begin with creating folder on our hard drive. Lets call it `DnD`, you are free to place it where you like. I have mine at `c:\apps\DnD`

Now create `Setup.goal`, where we prepare our database for all upcoming adventures. This step involves creating two essential tables: `games` and `story_lines`.

#### The Structure
- **Games Table**: Stores details of each D&D game like the name, books used, role, theme, tonality, character details, along with creation and update timestamps, and a brief summary.
- **Story Lines Table**: Tracks the narrative progress within each game, linking back to the `games` table via `game_id`. It includes fields for the story text, summary, the user who added it, and a timestamp.

#### The `Setup.goal` Code
Here's the straightforward Plang code to set up our database:

```plang
Setup
- create table games, columns: 
    GAME(string),BOOKS(string),ROLE(string), 
    THEME(string), TONALITY(string), CHARACTER(string)
    Created(datetime, now), Updated(datetime, now), Summary(string)
- create table story_lines, columns:
    game_id(long), created(datetime, now), 
    story(string), summary(string), user(string)
```

This will create tables in the database .db/data.sqlite. Each step, starting with dash(-), is only execute one time during the lifetime of the application.

#### Executing the Setup
To implement this setup, navigate to your `DnD` folder, and run the following in your terminal:

```bash
plang exec Setup
```

This single command builds and runs `Setup.goal`, constructing the required database structure. It's a one-time setup that organizes your game data efficiently.

With `Setup.goal` in place, we've laid the groundwork for our assistant. Next, we'll explore `Start.goal`, transitioning from setup to user interaction and gameplay. 

### Embarking on the Adventure with `Start.goal`

With our database structure in place, we now turn our attention to `Start.goal`, the heart of our D&D Game Assistant's user interaction. This goal serves as the main entry point, guiding users through the process of either starting a new game or continuing with an existing storyline.

#### The User Prompt
`Start.goal` kicks off with setting up a string, `%askUser%`, which lays out the options available to the user in a friendly and accessible manner. 

```plang
Start
- set string var %askUser% to "What would you like to do?\n\n
    \t0. Create new DoD
    \---------------------------
    \t- Story Arc Id"
- call goal !GenerateAskUser
```
_The syntax of each step is not strict, you can write it anyway you like as long as the intent is the same_

The initial setup here is straightforward: we're initating the variable `%askUser%` with what the user would like to do next, with the options to create a new game or enter a Story Arc to continue an existing game.

To make the experience more dynamic and tailored, `Start.goal` includes a call to `!GenerateAskUser`. This sub-goal fetches a list of recent games from the database and appends them to the `%askUser%` prompt, providing a quick way for users to jump back into their ongoing adventures.

```plang
GenerateAskUser
- select * from games order by updated desc, write to %games%
- foreach item in %games%, call !GenerateItemAskUser

GenerateItemAskUser
- append to %askUser%, "\t- %item.id%\t%item.GAME% - %item.BOOKS% - %item.Updated.ToString("f")%"
```

This approach showcases the power of Plang in creating interactive and dynamic applications. By fetching and displaying a list of games, we're not just offering a static menu but a personalized experience that reflects the user's history.

#### Decoding the User's Choice
After presenting the options, `Start.goal` captures the user's input and directs the flow based on their decision:

```plang
- ask user "%askUser%\n\nType in Story Arc Id or 0 to Create new game"
    , must be number(long), write to %gameId%
- if %gameId% = 0, then call !CreateNewGame
- if %gameId% > 0, then call !ShowStoryLine gameId=%gameId%
```

Here, the user's choice is processed, leading to either the creation of a new game or the continuation of an existing storyline. This decision-making process is a critical component of the goal, guiding the user through the application's core functionalities.

#### The Full `Start.goal` Source Code
Here's the complete `Start.goal` for reference:

```plang
Start
- set string var %askUser% to "What would you like to do?\n\n
    \t0. Create new DoD
    \---------------------------
    \t- Story Arc Id"
- call goal !GenerateAskUser
- ask user "%askUser%\n\nType in Story Arc Id or 0 to Create new game"
    , must be number(long), write to %gameId%
- if %gameId% = 0, then call !CreateNewGame
- if %gameId% > 0, then call !ShowStoryLine gameId=%gameId%

GenerateAskUser
- select * from games order by updated desc, write to %games%
- foreach item in %games%, call !GenerateItemAskUser

GenerateItemAskUser
- append to %askUser%, "\t- %item.id%\t%item.GAME% - %item.BOOKS% - %item.Updated.ToString("f")%"
```

Check out the [full source code of Start.goal in the repository](https://github.com/PLangHQ/apps/blob/main/DnD/Start.goal)

### Crafting New Realms with `CreateNewGame.goal`

Transitioning from the introductory interactions of `Start.goal`, we now delve into the essence of our D&D Game Assistant with `CreateNewGame.goal`. This component is where the user's journey truly begins, as they set the stage for their unique adventure.

#### The Default Game Setup
In `CreateNewGame.goal`, we start by defining a default game setup. This setup includes the game edition, books to use, the role of the user (typically Dungeon Master), the game's theme and tonality, and a default character to get the story rolling.

```plang
CreateNewGame
- set default values 
    %GAME%: Dungeons & Dragons: 5th Edition
    %BOOKS%: Any Random Campaign Book
    %ROLE%: Dungeon Master
    %THEME%: High Fantasy
    %TONALITY%: Whimsical & Heroic
    %CHARACTER%: Sabrina, a human mage with a funny pet.
```

This step ensures that even users who might be overwhelmed by the plethora of choices in setting up a D&D game have a solid starting point, making the game accessible to newcomers and veterans alike.

The variables are used later when will build a story using the LLM by loading them into the [gameSetupSystem.txt](https://github.com/PLangHQ/apps/blob/main/DnD/llm/gameSetupSystem.txt)

#### Customizing the Experience
After laying out the defaults, `CreateNewGame.goal` invites the user to customize their setup. This is where the application becomes truly user-centric, allowing for personalization that tailors the game to the individual's preferences.

```plang
- ask user 'Before we start, choose your setup of the game
    GAME: Dungeons & Dragons: 5th Edition
    BOOKS: Any Random Campaign Book
    ROLE: Dungeon Master
    THEME: High Fantasy
    TONALITY: Whimsical & Heroic
    CHARACTER: Sabrina, a human mage with a funny pet.
    \n\n
    Leave it empty if you happy with the defaults.
    '
    write to %answer%
- if %answer% is not empty call !LoadPreferences
```

The assistant prompts the user to review and potentially modify the default settings. This step empowers the user to shape the game to their liking, enhancing engagement and investment in the game setup process.

#### Integrating User Preferences
Should the user decide to customize their game setup, `CreateNewGame.goal` employs a smart mechanism to apply these preferences through `!LoadPreferences`. This feature demonstrates the flexibility of Plang, catering to user inputs and dynamically adjusting the game setup.

```plang
LoadPreferences
- read llm/preferenceSystem.txt into %preferenceSystem%
- [llm] system: %preferenceSystem%
    user: %answer%
    scheme: {GAME:string,BOOKS:string,ROLE:string, THEME:string, TONALITY:string, CHARACTER:string}
```

This process involves reading a [preference system command](https://github.com/PLangHQ/apps/blob/main/DnD/llm/preferenceSystem.txt) and applying it to the user's input, ensuring the game is set up exactly how the user desires.

#### Finalizing the Game Setup
With the preferences set, the game details are stored in the database, and the stage is set for the adventure to begin.

```plang
- insert into games, %GAME%, %BOOKS%, %ROLE%, %THEME%, %TONALITY%, %CHARACTER%
    write to %gameId%
- call goal !StartGame
```

This step is pivotal as it saves the game configuration, making it retrievable for future sessions, and marks the transition from setup to actual gameplay.


After setting the stage with `CreateNewGame` and `LoadPreferences`, our D&D Game Assistant script in Plang continues with the following goals.

### Starting the Game: `StartGame`

`StartGame` kicks off the actual gameplay, setting the scene for the player's adventure.

```plang
StartGame
- write out 'Starting game, loading...'
- read llm/gameSetupSystem.txt, write to %gameSetupSystem%, load vars
- [llm] system: %gameSetupSystem%
        scheme: {story:string, summary:string}
- call goal !SaveStoryLine %user%="Let's play"
```

This section signals the game's start and prepares the system to [generate the initial storyline](https://github.com/PLangHQ/apps/blob/main/DnD/llm/gameSetupSystem.txt). It utilizes a game setup system, a set of logic and data that helps in crafting the game's narrative, and concludes by saving the introductory storyline.

### Creating Storylines: `CreateStoryLine`

`CreateStoryLine` is where new segments of the game's story are generated, building upon the game's ongoing narrative.

```plang
CreateStoryLine
- select GAME,BOOKS,ROLE, THEME, TONALITY, CHARACTER from games where id=%gameId%, 1 row
- select user as user_input, summary as story_line_summary, created from story_lines 
    where gameId=%gameId% order by id desc, 
    newest 5, 
    write to %summaries%
- read llm/gameSetupSystem.txt, write to %gameSetupSystem%, load vars
- read llm/gameSetupAssistant.txt, write to %gameSetupAssistant%, load vars
- write out 'Loading next story arc...'
- [llm] system:%gameSetupSystem%
    assistant: %gameSetupAssistant%
    user: %nextStep%
    model: 'gpt-4-0125-preview'
    tempature: 1
    topp: 1
    scheme: {story:string, summary:string}
- call goal !SaveStoryLine user=%nextStep%
```

This goal dynamically generates story arcs based on the game's context and the latest developments. It involves querying the game's current state and the most recent storylines to inform the next narrative segment.

### Saving Storylines: `SaveStoryLine`

Each new storyline generated is saved through `SaveStoryLine`, ensuring the game's narrative continuity.

```plang
SaveStoryLine
- insert into story_lines %gameId%, %story%, %summary%, %user%, write to %storyLineId%
- call goal !ShowStoryLine
```

This goal inserts the new story segment into the `story_lines` table and then proceeds to display this latest part of the story to the player.

### Displaying Storylines: `ShowStoryLine`

`ShowStoryLine` presents the most recent storyline to the player, immersing them in the unfolding narrative.

```plang
ShowStoryLine
- select story, sl.id as storyLineId from story_lines sl
    join games g on g.id=sl.game_id
    where g.id=%gameId%, order by sl.id desc, limit 1
- write out %story%
- ask user "\n(to exit the game, type in 'exit game')

    ?:", write to %nextStep%
- if %nextStep% == 'exit game' or empty (case insensitive), then call !Start, else !CreateStoryLine
```

This goal retrieves and displays the latest story segment, offering the player a chance to continue the adventure or exit the game. It's a pivotal point where the narrative's progression and player interaction converge.

Check out the [full source code of CreateNewGame.goal in the repository](https://github.com/PLangHQ/apps/blob/main/DnD/CreateNewGame.goal)

### Why Choose the Plang DnD Over ChapGPT online store?

Opting for the Plang DnD offers distinct advantages for enhancing your gaming experience:

- **Local Operation**: Runs directly on your machine, ensuring smooth performance and immediate access.
- **Full Data Control**: You oversee all data, allowing for personalized management and organization.
- **Privacy**: Your data stays private, with confidentiality ensured for everything beyond what's shared with OpenAI.
- **Ease of Customization**: Modify the app directly on your computer—no need for complex processes like GitHub PRs or issue submissions. If there's something you want to change or add, you can do it swiftly and simply.

This allows you to embrace the freedom and control of enhancing your D&D sessions with this assistant, tailored just for you.

### Conclusion and Resources

We've outlined how to build a D&D Game Assistant with Plang. 

For the full code and ready-to-use versions, check the following:

- **Full Code**: Access on [GitHub](https://github.com/PLangHQ/apps/tree/main/DnD).
- **Ready-Made App**: You can start using it by downloading [DnD.zip](https://github.com/PLangHQ/apps/tree/main/DnD/DnD.zip). This uses Plang LLM service, it is simpler and support the Plang programming language but more expensive
- **OpenAI Key Version**: If you have OpenAI key you can use that [DnD_OpenAI.zip](https://github.com/PLangHQ/apps/tree/main/DnD/DnD_OpenAI.zip). It is more complex registration but cheaper.
- **DnD instructions**: I got the instruction from [https://www.rpgprompts.com/post/dungeons-dragons-chatgpt-prompt](https://www.rpgprompts.com/post/dungeons-dragons-chatgpt-prompt)

### Development Time and Costs

Bringing the D&D Assistant to life took approximately 4 hours from idea concept to the current version, with a total cost of about $20. This includes the iterative process of development, where changing steps and frequent rebuilding are part of the journey. 

For a straightforward build, the cost should be around $5, showcasing the efficiency and affordability of creating personalized tools with Plang.

### Upcoming: Customizing Your D&D Assistant

Stay tuned for my next post where we'll explore customizing your D&D Game Assistant. 

We'll cover convert the immersive story telling into audio, send updates to your mobile for on-the-go adventure tracking, and discuss what other options could be done.