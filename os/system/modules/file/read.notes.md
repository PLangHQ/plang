Path — the file to read · say: the path, inline · builder: as the step writes it
Template — fill in %variables% inside the file's text before returning · say: `load vars` · builder: true only when the step asks for the %variables% in the file's text to be filled in
Returns — the file's content. A JSON file is navigable; it is parsed when first navigated.
