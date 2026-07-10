using System;
using System.Linq;
using System.Reflection;
var asm = Assembly.LoadFrom(@"E:\code\c++\UnityRTS\GodotRTS\.godot\mono\temp\bin\Debug\GodotSharp.dll");
foreach (var typeName in new[]{"Godot.ItemList","Godot.LineEdit","Godot.SpinBox","Godot.OptionButton","Godot.CheckBox","Godot.Button","Godot.EditorPlugin+DockSlot","Godot.DockSlot"})
{
    var t = asm.GetType(typeName);
    Console.WriteLine("TYPE=" + typeName + " => " + (t?.FullName ?? "NOT_FOUND"));
    if (t == null) continue;
    foreach (var ev in t.GetEvents(BindingFlags.Instance | BindingFlags.Public).OrderBy(e => e.Name))
        Console.WriteLine("  EVENT " + ev.Name + " : " + ev.EventHandlerType?.FullName);
    foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.Name.Contains("Selected") || p.Name.Contains("Value") || p.Name.Contains("Text") || p.Name.Contains("Button") || p.Name.Contains("Pressed") || p.Name.Contains("Item" )).OrderBy(p => p.Name))
        Console.WriteLine("  PROP " + p.PropertyType.Name + " " + p.Name);
    foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(m => m.Name.Contains("AddItem") || m.Name.Contains("Clear") || m.Name.Contains("Select") || m.Name.Contains("Deselect") || m.Name.Contains("GetSelected") || m.Name.Contains("SetItem") || m.Name.Contains("GetItem") || m.Name.Contains("GetLineEdit") ).OrderBy(m => m.Name))
        Console.WriteLine("  METH " + m);
}
