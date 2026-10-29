Step text: `read file.txt, write to %content%`
Formal: `file.read(Path: path = "file.txt")` — the trailing `write to %content%` is its own action.

Step text: `read 'config/settings.json'`
Formal: `file.read(Path: path = "config/settings.json")`

Step text: `read 'receipt.txt', load vars, write to %receipt%`
Formal: `file.read(Path: path = "receipt.txt", Template: choice<template> = "plang")` — "load vars" (also "fill in the variables", "with variables") makes the file's text a plang template, its %variables% filled from memory; a plain read with none of these leaves Template out. The trailing `write to %receipt%` is its own action.
