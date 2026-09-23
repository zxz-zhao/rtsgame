using Godot;

public static class CommandLineArgs
{
    public static string[] Get()
    {
        var userArgs = OS.GetCmdlineUserArgs();
        var mainArgs = OS.GetCmdlineArgs();
        var list = new System.Collections.Generic.List<string>(mainArgs);
        list.AddRange(userArgs);
        return list.ToArray();
    }
}
