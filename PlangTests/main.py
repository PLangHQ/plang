# simple_add.py

import importlib.util
import __main__

def add(a, b):
    return a + b

def check_numpy_installed():
    package_name = 'numpy'
    package_spec = importlib.util.find_spec(package_name)
    if package_spec is None:
        print(f"The required package {package_name} is not installed.")
        exit(1)


def main():
    check_numpy_installed()
    global result
    
    num1 = float(3)
    num2 = float(3)
    
    result = add(num1, num2)
    
    print(f"The sum of {num1} and {num2} is {result}.")
    return result


main()