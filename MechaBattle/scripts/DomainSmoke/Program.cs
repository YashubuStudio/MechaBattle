using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;
using MechaBattle.Battlefield;
using MechaBattle.Tests;

// Runs the pure C# regression cases without launching Unity. This is deliberately
// reported as a smoke harness, NOT an official NUnit/Unity Test Runner execution.
var project = Path.GetFullPath(args[0]);
var cases = new List<object>();
int passed = 0, failed = 0;
foreach (var fixtureType in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Namespace == "MechaBattle.Tests" && t.IsClass && t.GetMethods().Any(m => m.IsDefined(typeof(TestAttribute)) || m.IsDefined(typeof(TestCaseAttribute)))).OrderBy(t => t.Name))
{
var fixture = Activator.CreateInstance(fixtureType);
foreach (var method in fixtureType.GetMethods().OrderBy(m => m.Name))
{
    var attributes = method.GetCustomAttributes<TestCaseAttribute>().ToArray();
    if (attributes.Length == 0 && method.GetCustomAttribute<TestAttribute>() == null) continue;
    var inputs = attributes.Length == 0 ? new[] { Array.Empty<object>() } : attributes.Select(a => a.Arguments);
    foreach (var input in inputs)
    {
        var label = method.Name + "(" + string.Join(",", input) + ")";
        try { method.Invoke(fixture, input); passed++; cases.Add(new { name = label, status = "passed" }); }
        catch (Exception ex)
        {
            failed++; cases.Add(new { name = label, status = "failed", error = (ex.InnerException ?? ex).ToString() });
        }
    }
}
}
try
{
    var map = JsonSerializer.Deserialize<MapDefinition>(File.ReadAllText(Path.Combine(project, "config/standard-map.json")), new JsonSerializerOptions { IncludeFields = true });
    MapValidator.ThrowIfInvalid(map);
    if (map.mainBases.Count(b => b.team == 0) != 3 || map.mainBases.Count(b => b.team == 1) != 3)
        throw new Exception("Standard map must have three main bases per team.");
    passed++; cases.Add(new { name = "StandardJsonMap_HasThreeMainBasesPerTeam", status = "passed" });
}
catch (Exception ex) { failed++; cases.Add(new { name = "StandardJsonMap_HasThreeMainBasesPerTeam", status = "failed", error = ex.ToString() }); }
try { var battle = HeadlessBattleSmoke.Run(project); passed++; cases.Add(battle); }
catch (Exception ex) { failed++; cases.Add(new { name = "ActualConfig_100SlotPublicObjectiveBattle", status = "failed", error = ex.ToString() }); }
try { var battle = ActualBotDefaultsSmoke.Run(project); passed++; cases.Add(battle); }
catch (Exception ex) { failed++; cases.Add(new { name = "ActualClaudeBot_ActualConfig_100Slots", status = "failed", error = ex.ToString() }); }
var output = Path.Combine(project, "Build/Reports/domain-smoke.json");
Directory.CreateDirectory(Path.GetDirectoryName(output));
var report = JsonSerializer.Serialize(new { scope = "pure-csharp-smoke-not-unity-test-runner", executedUtc = DateTime.UtcNow, passed, failed, cases }, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(output, report);
Console.WriteLine(report);
return failed == 0 ? 0 : 1;
