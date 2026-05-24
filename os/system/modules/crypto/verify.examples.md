Step text: `verify %content% against %hash%, write to %isValid%`
Mapping: `crypto.verify Data([object] %content%), Hash([string] %hash%), Algorithm([string] keccak256) | variable.set Name([string] %isValid%), Value([object] %!data%)`
