namespace HakoJS.VM;

public enum ValueLifecycle
{
    Owned,
    Borrowed,
    Temporary
}