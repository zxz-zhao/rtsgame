using System;
using System.Linq;
using System.Reflection;
foreach (var path in new[]{@"E:\code\c++\UnityRTS\GodotRTS\.godot\mono\temp\bin\Debug\GodotSharp.dll", @"E:\code\c++\UnityRTS\GodotRTS\.godot\mono\temp\bin\Debug\GodotSharpEditor.dll"})
{
    var asm = Assembly.LoadFrom(path);
    Console.WriteLine("ASM=" + path);
    foreach (var t in asm.GetTypes().Where(t => (t.FullName ?? "").Contains("DockSlot") || t.Name.Contains("Dock") || (t.FullName ?? "").Contains("EditorDock")))
    {
        Console.WriteLine(t.FullName);
        if (t.IsEnum)
        {
            foreach (var name in Enum.GetNames(t))
                Console.WriteLine("  " + name + "=" + Convert.ToInt32(Enum.Parse(t, name)));
        }
    }
}
