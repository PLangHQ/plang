Path — the file to read · say: the path, inline · builder: as the step writes it
Variables — fill in the %variables% written inside the file's text before returning · say: load vars, fill in the variables, with variables · builder: true when the step says to load or fill the file's variables ("load vars", "fill in the variables", "with variables"); a plain read says none of these and leaves it false
Returns — the file's content. A JSON file is navigable; it is parsed when first navigated.
