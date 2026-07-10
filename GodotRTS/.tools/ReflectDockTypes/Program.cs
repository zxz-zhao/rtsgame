using System;
using System.Linq;
using System.Reflection;
var asm = Assembly.LoadFrom(@"E:\code\c++\UnityRTS\GodotRTS\.godot\mono\temp\bin\Debug\GodotSharp.dll");
foreach (var t in asm.GetTypes().Where(t => t.Name.Contains("DockSlot") || t.FullName.Contains("DockSlot") || t.Name.Contains("Dock")))
{
    Console.WriteLine(t.FullName);
    if (t.IsEnum)
    {
        foreach (var name in Enum.GetNames(t))
            Console.WriteLine("  " + name + "=" + Convert.ToInt32(Enum.Parse(t, name)));
    }
}
