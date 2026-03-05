using System;
using System.Collections.Generic;

[Serializable]
public class MessageBubbleData
{
    public string text;
    public bool requiresResponse;

    public List<string> options;  // if empty = no options

    public string chosenOption;
}