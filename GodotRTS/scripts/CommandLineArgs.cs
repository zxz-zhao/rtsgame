using Godot;

public static class CommandLineArgs
{
    public static string[] Get()
    {
        var userArgs = OS.GetCmdlineUserArgs();
        return userArgs.Length > 0 ? userArgs : OS.GetCmdlineArgs();
    }
}
