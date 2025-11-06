namespace HakoJS.VM;

public enum JSErrorType
{
    Range = 0,
    Reference = 1,
    Syntax = 2,
    Type = 3,
    Uri = 4,
    Internal = 5,
    OutOfMemory = 6
}