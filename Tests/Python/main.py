import sys
import __main__

def add_two_numbers(num1, num2):
    return num1 + num2

def main():  
    number1 = float(sys.argv[1])
    number2 = float(sys.argv[2])
    return add_two_numbers(number1, number2)

result = main()
