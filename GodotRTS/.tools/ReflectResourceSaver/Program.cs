using System;
using System.Linq;
using System.Reflection;
var asm = Assembly.LoadFrom(@"E:\code\c++\UnityRTS\GodotRTS\.godot\mono\temp\bin\Debug\GodotSharp.dll");
var t = asm.GetType("Godot.ResourceSaver");
Console.WriteLine(t == null ? "TYPE_NOT_FOUND" : t.FullName);
if (t != null)
{
    foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Public).OrderBy(m => m.Name))
    {
        if (m.Name.Contains("Save") || m.Name.Contains("GetRecognizer"))
            Console.WriteLine(m);
    }
}
