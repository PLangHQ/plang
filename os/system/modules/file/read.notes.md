Path — the file to read · say: the path, inline · builder: as the step writes it
Template — the kind of template the file's text is: plang fills its %variables% from memory · say: load vars, fill in the variables, with variables · builder: plang when the step says to load or fill the file's variables ("load vars", "fill in the variables", "with variables"); a plain read says none of these and leaves Template out
Returns — the file's content. A JSON file is navigable; it is parsed when first navigated.
