

string FormatChat(string text)
{
    var matches = Regex.Matches(text, @"^\s*(\{[\s\S]*?\}|\[[\s\S]*?\])\s*$");
    for (int i = 0; i < matches.Count; i++)
    {
        


    }
    
    // \[\s*\{\s*"name"\s*:\s*".*?"\s*\}\s*\]

}