using System;
using System.Linq;
using System.Reflection;

void Dump(string typeName)
{
    var asm1 = Assembly.LoadFrom(@"E:\code\c++\UnityRTS\GodotRTS\.godot\mono\temp\bin\Debug\GodotSharp.dll");
    var asm2 = Assembly.LoadFrom(@"E:\code\c++\UnityRTS\GodotRTS\.godot\mono\temp\bin\Debug\GodotSharpEditor.dll");
    var t = asm2.GetType(typeName) ?? asm1.GetType(typeName);
    Console.WriteLine(t == null ? $"TYPE_NOT_FOUND:{typeName}" : t.FullName);
    if (t == null) return;
    foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(m => m.Name.Contains("Dock") || m.Name.Contains("ControlTo") || m.Name.Contains("SceneFromPath") || m.Name.Contains("SaveScene") || m.Name.Contains("EditedScene") || m.Name.Contains("Selection") || m.Name.Contains("Inspect") || m.Name.Contains("EditResource") || m.Name.Contains("MarkScene"))
        .OrderBy(m => m.Name))
    {
        Console.WriteLine(m.ToString());
    }
}

Dump("Godot.EditorPlugin");
Console.WriteLine("---");
Dump("Godot.EditorInterface");
