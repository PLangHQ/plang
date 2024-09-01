# Time Handling in Plang

In Plang, time can be accessed and manipulated using specific syntax that leverages the underlying C# `DateTime` object. This documentation will guide you through accessing the current time, modifying time values, and utilizing various `DateTime` methods and properties.

## Accessing Current Time

In Plang, `%Now%` is a reserved keyword that represents the current date and time, equivalent to the C# object `DateTime.Now`. Similarly, `%NowUtc%` corresponds to `DateTime.UtcNow`.

### Example

```plang
Start
- write out %Now%
```

This will output the current date and time based on the system's local settings.

## Setting Specific Dates and Times

You can set specific dates and times using the `SetTime` goal. This allows you to define past or future dates and times.

### Example

```plang
SetTime
- set %pastDate% = %Now=2000-12-31%
- set %pastTime% = %Now=2000-12-31T24:30:45%
```

In this example, `%pastDate%` is set to December 31, 2000, and `%pastTime%` is set to a specific time on that date.

## Modifying Time

Plang allows you to easily modify time values by adding or subtracting time intervals.

### Example

```plang
Modify
- set %oneDay% = %Now+1day%
- set %ms% = %Now+115ms%
- set %5years% = %Now+5years%
- set %2daysAgo% = %Now-2days%
- set %53secAgo% = %Now-53secs%
- set %15yearsAgo% = %Now-15years%
```

In this example, various time modifications are performed, such as adding days, milliseconds, and years, or subtracting days, seconds, and years from the current time.

## Working with DateTime Methods and Properties

Developers can use all methods and properties from the C# `DateTime` class. For a comprehensive list, refer to the [Microsoft documentation](https://learn.microsoft.com/en-us/dotnet/api/system.datetime?view=net-8.0).

### Common Properties and Methods

- **AddYears(int years)**: Adds a specified number of years to the current date.
- **DayOfWeek**: Gets the day of the week represented by the current date.

### Example

```plang
WorkWithNow
- set variable %futureDate% to %Now.AddYears(1)%
- set variable %dayOfWeek% to %Now.DayOfWeek%
```

In this example, `%futureDate%` is set to one year from the current date, and `%dayOfWeek%` retrieves the current day of the week.

## Formatting Dates and Times

The `ToString(string)` method allows you to format dates and times according to specific patterns. The format follows the culture set on the device, which can be changed if needed.

### Example

```plang
Start
- write out %Now.ToString("d")%
```

This will print the date in the short date pattern format. Here are some common format specifiers:

- `d`: Short date pattern (e.g., 6/15/2008)
- `D`: Long date pattern (e.g., Sunday, June 15, 2008)
- `f`: Full date/time pattern (short time) (e.g., Sunday, June 15, 2008 9:15 PM)
- `F`: Full date/time pattern (long time) (e.g., Sunday, June 15, 2008 9:15:07 PM)
- `g`: General date/time pattern (short time) (e.g., 6/15/2008 9:15 PM)
- `G`: General date/time pattern (long time) (e.g., 6/15/2008 9:15:07 PM)
- `m`: Month day pattern (e.g., June 15)
- `o`: Round-trip date/time pattern (e.g., 2008-06-15T21:15:07.0000000)
- `R`: RFC1123 pattern (e.g., Sun, 15 Jun 2008 21:15:07 GMT)
- `s`: Sortable date/time pattern (e.g., 2008-06-15T21:15:07)
- `t`: Short time pattern (e.g., 9:15 PM)
- `T`: Long time pattern (e.g., 9:15:07 PM)
- `u`: Universal sortable date/time pattern (e.g., 2008-06-15 21:15:07Z)
- `U`: Universal full date/time pattern (e.g., Monday, June 16, 2008 4:15:07 AM)
- `y`: Year month pattern (e.g., June, 2008)

### Changing Culture

If the date and time format does not appear as expected, you may need to change the culture setting.

```plang
Start
- set culture to Icelandic
- write out %Now.ToString("d")%
```

This will print out '1.12.2024' (is-IS) instead of '12/1/2024' (en-US) if your computer is set to en-US. For more information on culture settings, refer to the [CultureInfo documentation](./modules/README.md#cultureinfo).

## Next Steps

Now that you have an understanding of variables, conditions, loops, and time in Plang, let's start creating an app. Check out the [Todo app documentation](./Todo_webservice.md) to get started.