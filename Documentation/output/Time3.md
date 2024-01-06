# Time Management in plang

Time management is an essential feature in programming, and plang provides a straightforward way to work with time using the reserved keyword `%Now%`. This keyword is a direct representation of the C# `DateTime.UtcNow` object, allowing developers to handle UTC time efficiently within their plang applications.

## Accessing Current Time

In plang, the current UTC time can be accessed with the `%Now%` keyword. This reserved keyword is equivalent to the C# `DateTime.UtcNow` and is used as follows:

```plang
%Now%
```

## Setting a Specific Time

To set a specific time in plang, you can assign a date and time to the `%Now%` keyword using the following formats:

```plang
Set time
    - %Now=2000-12-31%
    - %Now=2000-12-31T24:30:45%
```

The first format sets the date, while the second format includes both the date and time.

## Modifying Time

plang allows you to modify the current time by adding or subtracting time units. Here are some examples of how to do this:

```plang
- %Now+1day%       // Adds one day to the current time
- %Now+115ms%      // Adds 115 milliseconds to the current time
- %Now+5years%     // Adds five years to the current time
- %Now-2days%      // Subtracts two days from the current time
- %Now-53secs%     // Subtracts 53 seconds from the current time
- %Now-15years%    // Subtracts fifteen years from the current time
```

## Utilizing DateTime Methods and Properties

Developers can leverage all the methods and properties from the `DateTime` class in their plang code. The official documentation for `DateTime` is available at [Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.datetime?view=net-8.0).

### Commonly Used Properties and Methods

Below are examples of commonly used `DateTime` properties and methods in plang, excluding `.Now` and `.UtcNow`:

#### AddYears Method

Adds a specified number of years to the date.

```plang
WorkWithNow
- set variable %futureDate% to %Now.AddYears(1)%
```

#### DayOfWeek Property

Gets the day of the week represented by the current `DateTime`.

```plang
WorkWithNow
- set variable %dayOfWeek% to %Now.DayOfWeek%
```

#### AddDays Method

Adds a specified number of whole days to the date.

```plang
- set variable %10DaysFromNow% to %Now.AddDays(10)%
```

#### AddHours Method

Adds a specified number of whole hours to the time.

```plang
- set variable %5HoursFromNow% to %Now.AddHours(5)%
```

#### ToString Method

Converts the value of the current `DateTime` object to its equivalent string representation.

```plang
- %DateString= %Now.ToString("yyyy-MM-dd HH:mm:ss")%
```

## Conclusion

With the `%Now%` keyword and the ability to use all methods and properties from the `DateTime` class, plang provides a robust set of tools for managing time in your applications. Whether you need to set a specific time, modify it, or perform complex date and time calculations, plang simplifies these tasks with its intuitive syntax and direct access to powerful `DateTime` functionalities.