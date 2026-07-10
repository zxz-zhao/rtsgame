using System;
using System.Linq;
using System.Reflection;
var asm1 = Assembly.LoadFrom(@"E:\code\c++\UnityRTS\GodotRTS\.godot\mono\temp\bin\Debug\GodotSharp.dll");
var t = asm1.GetType("Godot.EditorPlugin+DockSlot");
Console.WriteLine(t == null ? "TYPE_NOT_FOUND" : t.FullName);
if (t != null)
{
    foreach (var name in Enum.GetNames(t))
        Console.WriteLine(name + "=" + Convert.ToInt32(Enum.Parse(t, name)));
}
