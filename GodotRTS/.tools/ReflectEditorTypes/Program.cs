using System;
using System.Linq;
using System.Reflection;
foreach (var path in new[]{@"E:\code\c++\UnityRTS\GodotRTS\.godot\mono\temp\bin\Debug\GodotSharp.dll", @"E:\code\c++\UnityRTS\GodotRTS\.godot\mono\temp\bin\Debug\GodotSharpEditor.dll"})
{
    var asm = Assembly.LoadFrom(path);
    Console.WriteLine("ASM=" + asm.FullName);
    foreach (var name in asm.GetTypes().Where(t => (t.FullName ?? "").Contains("EditorPlugin") || (t.FullName ?? "").Contains("DockSlot") || (t.FullName ?? "").Contains("CustomControlContainer")).Select(t => t.FullName).OrderBy(x => x))
        Console.WriteLine(name);
}
