using System;
using System.Linq;
using System.Reflection;
var asm1 = Assembly.LoadFrom(@"E:\code\c++\UnityRTS\GodotRTS\.godot\mono\temp\bin\Debug\GodotSharp.dll");
var asm2 = Assembly.LoadFrom(@"E:\code\c++\UnityRTS\GodotRTS\.godot\mono\temp\bin\Debug\GodotSharpEditor.dll");
var t = asm2.GetType("Godot.EditorPlugin") ?? asm1.GetType("Godot.EditorPlugin");
Console.WriteLine(t?.FullName ?? "TYPE_NOT_FOUND");
if (t != null)
{
    foreach (var ev in t.GetEvents(BindingFlags.Instance | BindingFlags.Public).OrderBy(e => e.Name))
        Console.WriteLine(ev.Name + " : " + ev.EventHandlerType?.FullName);
}
