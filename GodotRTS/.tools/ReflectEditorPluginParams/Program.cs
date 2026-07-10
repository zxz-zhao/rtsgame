using System;
using System.Linq;
using System.Reflection;
var asm = Assembly.LoadFrom(@"E:\code\c++\UnityRTS\GodotRTS\.godot\mono\temp\bin\Debug\GodotSharp.dll");
var t = asm.GetType("Godot.EditorPlugin");
foreach (var m in t!.GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(m => m.Name == "AddControlToDock" || m.Name == "AddControlToContainer"))
{
    Console.WriteLine(m);
    foreach (var p in m.GetParameters())
    {
        Console.WriteLine($"  {p.Name}: {p.ParameterType.FullName}");
        if (p.ParameterType.IsEnum)
        {
            foreach (var name in Enum.GetNames(p.ParameterType))
                Console.WriteLine($"    {name}={Convert.ToInt32(Enum.Parse(p.ParameterType, name))}");
        }
    }
}
