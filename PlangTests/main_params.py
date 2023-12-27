import argparse 

def add(a, b):
    return a + b


def main(args):
    global result
    
    num1 = float(args.num1)
    num2 = float(args.num2)
    
    result = add(num1, num2)
    
    print(f"The sum of {num1} and {num2} is {result}.")
    return result

parser = argparse.ArgumentParser(description='Add two numbers.')
parser.add_argument('--num1', required=True, help='The first number')
parser.add_argument('--num2', required=True, help='The second number')

args = parser.parse_args()
main(args)