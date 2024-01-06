# Time Management in plang

Managing time and dates is a crucial aspect of many applications. In plang, you can easily handle time by using the reserved keyword `%Now%`, which represents the current UTC time and is equivalent to the C# `DateTime.UtcNow` object. Below is a guide on how to set and modify time in plang, as well as how to utilize the various methods and properties available from the `DateTime` class.

## Setting the Current Time

To set the current time in plang, you can use the following syntax:

```plang
Set time
    - %Now=2000-12-31%
    - %Now=2000-12-31T24:30:45%
```

In the above example, `%Now%` is a variable that represents the current time. You can assign it a specific date and time in the format `YYYY-MM-DD` or `YYYY-MM-DDTHH:MM:SS`.

## Modifying Time

plang allows you to modify time by adding or subtracting time units such as microseconds, milliseconds, seconds, minutes, hours, days, months, and years. Here are some examples:

```plang

```

## Using DateTime Methods and Properties

Developers can use all the methods and properties from the `DateTime` class in their plang code. The official documentation for `DateTime` can be found at [Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.datetime?view=net-8.0).

### Common Properties and Methods

Here are some of the most commonly used properties and methods from the `DateTime` class, along with examples for each:

#### Now Property

Gets the current date and time.

```plang
- %CurrentTime=6.1.2024 17:19:58%
```

#### UtcNow Property

Gets the current UTC date and time.

```plang
- %CurrentUtcTime=6.1.2024 17:19:58%
```

#### AddDays Method

Adds a specified number of whole days to the date.

```plang
- %FutureDate=%Now%.AddDays(10)%
```

#### AddHours Method

Adds a specified number of whole hours to the time.

```plang
- %FutureTime=%Now%.AddHours(5)%
```

#### ToString Method

Converts the value of the current `DateTime` object to its equivalent string representation.

```plang
- %DateString=%Now%.ToString("yyyy-MM-dd HH:mm:ss")%
```

### Platform-Specific Examples

#### Windows

```plang
- %WinCurrentTime=6.1.2024 17:19:58%
```

#### Linux

```plang
- %LinuxCurrentTime=6.1.2024 17:19:58%
```

#### macOS

```plang
- %MacCurrentTime=6.1.2024 17:19:58%
```

Remember to replace `6.1.2024 17:19:58` with the actual syntax for obtaining the current time in plang when implementing these examples.

## Conclusion

With this guide, you should now have a good understanding of how to manage time within plang. Whether you're setting a specific time, modifying it, or utilizing the `DateTime` class's methods and properties, plang provides a straightforward approach to handling time in your applications.