# Time Management in plang

Managing time effectively is crucial in software development, and plang provides a powerful yet simple way to handle time through the reserved keyword `%Now%` and `%NowUTC%`. This keyword represents the current time and in UTC, mirroring the C# `DateTime.Now` and `DateTime.UtcNow` object, and allows developers to perform various time-related operations with ease.

## Accessing Current Time

To access the current time in plang, use the reserved keyword `%Now%`. This keyword is directly linked to the C# `DateTime.Now` object and can be used as shown below:

```plang
- %Now%
- %NowUtc%
```

## Setting a Specific Time

You can set a specific time in plang by assigning a date and time string to the `%Now%` keyword. The syntax for setting time is as follows:

```plang
Set time
    - %Now=2000-12-31%
    - %Now=2000-12-31T24:30:45%
```

The first line sets the date, while the second line includes both the date and time.

## Modifying Time

plang allows you to modify the current time by adding or subtracting time units. Here's how you can do it:

```plang
- %Now+1day%       // Adds one day to the current time
- %Now+115ms%      // Adds 115 milliseconds to the current time
- %Now+5years%     // Adds five years to the current time
- %Now-2days%      // Subtracts two days from the current time
- %Now-53secs%     // Subtracts 53 seconds from the current time
- %Now-15years%    // Subtracts fifteen years from the current time
```

## Utilizing DateTime Methods and Properties

Developers can use all the methods and properties from the `DateTime` class in their plang code. The official documentation for `DateTime` is available at [Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.datetime?view=net-8.0).

### Commonly Used Properties and Methods

Here are examples of commonly used `DateTime` properties and methods in plang, with the exception of `.Now` and `.UtcNow`:

#### AddYears Method

This method adds a specified number of years to the date.

```plang
WorkWithNow
- set variable %futureDate% to %Now.AddYears(1)%
```

#### DayOfWeek Property

This property retrieves the day of the week represented by the current `DateTime`.

```plang
WorkWithNow
- set variable %dayOfWeek% to %Now.DayOfWeek%
```

## Formatting DateTime with ToString

The `ToString` method converts the value of the current `DateTime` object to its equivalent string representation. The format follows the culture set on the device, which can be changed using the following plang code:

```plang
ChangeCulture
- set culture to is-IS
```

Here are various string formats you can use with the `ToString` method:

```plang
- write out %Now.ToString("d")%       // 6/15/2008
- write out %Now.ToString("D")%       // Sunday, June 15, 2008
- write out %Now.ToString("f")%       // Sunday, June 15, 2008 9:15 PM
- write out %Now.ToString("F")%       // Sunday, June 15, 2008 9:15:07 PM
- write out %Now.ToString("g")%       // 6/15/2008 9:15 PM
- write out %Now.ToString("G")%       // 6/15/2008 9:15:07 PM
- write out %Now.ToString("m")%       // June 15
- write out %Now.ToString("o")%       // 2008-06-15T21:15:07.0000000
- write out %Now.ToString("R")%       // Sun, 15 Jun 2008 21:15:07 GMT
- write out %Now.ToString("s")%       // 2008-06-15T21:15:07
- write out %Now.ToString("t")%       // 9:15 PM
- write out %Now.ToString("T")%       // 9:15:07 PM
- write out %Now.ToString("u")%       // 2008-06-15 21:15:07Z
- write out %Now.ToString("U")%       // Monday, June 16, 2008 4:15:07 AM
- write out %Now.ToString("y")%       // June, 2008
- write out %Now.ToString("'h:mm:ss.ff t'")% // 9:15:07.00 P
- write out %Now.ToString("'d MMM yyyy'")%   // 15 Jun 2008
- write out %Now.ToString("'HH:mm:ss.f'")%   // 21:15:07.0
- write out %Now.ToString("'dd MMM HH:mm:ss'")% // 15 Jun 21:15:07
- write out %Now.ToString("'\\Mon\\t\\h\\: M'")% // Month: 6
- write out %Now.ToString("'HH:mm:ss.ffffzzz'")% // 21:15:07.0000-07:00
```

By using `%Now%` and the various methods and properties of the `DateTime` class, plang developers have a comprehensive toolkit for managing and formatting time in their applications.