
# Python in PLang
## Introduction
Python is a versatile and widely-used programming language known for its readability and straightforward syntax. It's a great choice for beginners and experienced developers alike, as it allows for quick development and prototyping.

## For Beginners
Python is like a magic wand for computers. Just as a magician uses a wand to make things happen, you can use Python to tell a computer what to do. It's simple to learn because it's written more like the English language than other programming languages. You can use Python to solve problems, analyze data, create websites, and even build games!

## Best Practices for Python in PLang
When writing Python code in PLang, it's important to keep your code clean and well-organized. Here's an example of a best practice:

- **Use descriptive variable names**: This makes your code easier to read and understand.

```plang
Python
- set var %file_path% to 'data.csv'
- call analyze_data.py, 'file %file_path%', write to %results%
- write out 'Analysis results: %results%'
```

In this example, `%file_path%` is a descriptive variable name that tells us it contains the path to a file. The `analyze_data.py` script is called with a clear parameter, and the results are stored in a variable named `%results%`, which indicates what it contains.


# Python Module Documentation

The Python module in PLang allows users to run Python scripts with various parameters and options. Below are examples of how to use the Python module in PLang, sorted by their popularity and typical use cases.

## Examples

### 1. Running a Python Script Without Parameters
This is the most basic usage, where you simply want to execute a Python script without passing any parameters.

```plang
Python
- call main.py, write to %output%
- write out 'Script output: %output%'
```

### 2. Running a Python Script With Parameters
In cases where you need to pass parameters to your Python script, you can specify them in a natural language format.

```plang
Python
- set var %filename% to 'data_analysis.py'
- set var %data_file% to 'dataset.csv'
- set var %max_size% to '50mb'
- call %filename%, %data_file%, 'max size %max_size%', write to %analysis_results%
- write out 'Analysis Results: %analysis_results%'
```

### 3. Running a Python Script With Named Arguments
When your Python script expects named arguments, you can enable this feature in the call.

```plang
Python
- set var %script% to 'process_images.py'
- set var %input_folder% to '/images/raw'
- set var %output_folder% to '/images/processed'
- set var %resize% to '800x600'
- call %script%, 'input folder %input_folder%', 'output folder %output_folder%', 'resize %resize%', use named arguments, write to %processed_images%
- write out 'Processed images info: %processed_images%'
```

### 4. Extracting Variables from a Python Script
If you need to extract variables from the Python script after execution, you can specify which variables to extract.

```plang
Python
- set var %script_to_run% to 'extract_data.py'
- set var %variables_to_extract% to ['user_count', 'transaction_total']
- call %script_to_run%, variables to extract %variables_to_extract%, write to %extracted_data%
- write out 'Data extracted: %extracted_data%'
```

### 5. Running a Python Script in a Terminal
For scripts that require a terminal environment or for debugging purposes, you can run the script in a terminal.

```plang
Python
- set var %development_script% to 'debug_tool.py'
- call %development_script%, use terminal, write to %debug_output%
- write out 'Debug output: %debug_output%'
```

### 6. Specifying a Custom Python Path
If you need to run the script with a specific Python interpreter, you can specify the path to the Python executable.

```plang
Python
- set var %custom_python% to '/opt/python3.8/bin/python'
- set var %script_name% to 'custom_env_script.py'
- call %script_name%, python path %custom_python%, write to %custom_env_output%
- write out 'Output with custom Python: %custom_env_output%'
```

### 7. Capturing Standard Output and Error
To capture the standard output and error streams from the Python script, you can specify variable names to store these streams.

```plang
Python
- set var %error_prone_script% to 'might_fail.py'
- call %error_prone_script%, std out variable name 'stdout', std error variable name 'stderr', write to %script_status%
- write out 'Script output: %script_status[stdout]%'
- write out 'Script error (if any): %script_status[stderr]%'
```

These examples demonstrate various ways to use the Python module in PLang, catering to different scenarios and requirements. Users can adapt these examples to fit their specific use cases.


For a full list of examples, visit [PLang Python Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Python).

## Step Options
When writing your PLang code, you can enhance your steps with these options. Click on the links for more details on how to use each one:

- [CacheHandler](/moduels/cacheHandler.md)
- [ErrorHandler](/moduels/ErrorHandler.md)
- [RetryHandler](/moduels/RetryHandler.md)
- [CancelationHandler](/moduels/CancelationHandler.md)
- [Run and Forget](/moduels/RunAndForget.md)

## Advanced
For those who want to dive deeper and understand how PLang's Python module works under the hood with C#, check out the [advanced documentation](./PLang.Modules.PythonModule_advanced.md).

## Created
This documentation was created on 2024-01-02T22:15:49.
