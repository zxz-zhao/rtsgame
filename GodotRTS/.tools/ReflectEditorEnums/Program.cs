using System;
using System.Linq;
using System.Reflection;
var asm = Assembly.LoadFrom(@"E:\code\c++\UnityRTS\GodotRTS\.godot\mono\temp\bin\Debug\GodotSharpEditor.dll");
foreach (var typeName in new[]{"Godot.EditorDock+DockSlot","Godot.EditorPlugin+DockSlot","Godot.EditorPlugin+CustomControlContainer"})
{
    var t = asm.GetType(typeName);
    Console.WriteLine("TYPE=" + typeName + " => " + (t?.FullName ?? "NOT_FOUND"));
    if (t != null && t.IsEnum)
    {
        foreach (var name in Enum.GetNames(t))
            Console.WriteLine("  " + name + "=" + Convert.ToInt32(Enum.Parse(t, name)));
    }
}
