using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace RTDSimulator.Core;

/// <summary>
/// Generates message payloads from a template. The template can contain:
/// <list type="bullet">
/// <item>Variables, e.g. <c>{{UserId}}</c>, resolved from <see cref="Variables"/>.</item>
/// <item>C# expressions, e.g. <c>{{$DateTime.Now}}</c>, evaluated via Roslyn scripting.</item>
/// </list>
/// This type is service-agnostic and lives in Core so it can be reused by any connector.
/// </summary>
public class PayloadGenerator
{
    /// <summary>The (soft-coded) variable definitions applied to the template.</summary>
    public IReadOnlyList<VariableDefinition> Variables { get; }

    private readonly string _payload;
    private readonly Dictionary<string, string> Expressions = new Dictionary<string, string>();
    private ConcurrentDictionary<string, Script<Object>> preCompiledScripts { get; set; } = new ConcurrentDictionary<string, Script<Object>>();
    private readonly ScriptFunctions _functions = new ScriptFunctions();

    /// <summary>Creates a generator using the built-in default variable definitions.</summary>
    public PayloadGenerator(string payload)
        : this(payload, VariableDefinitions.LoadDefaults())
    {
    }

    /// <summary>Creates a generator using the supplied variable definitions.</summary>
    public PayloadGenerator(string payload, IEnumerable<VariableDefinition> variables)
    {
        _payload = payload;
        Variables = variables.ToList();

        // Finding expressions...
        // Example of an expression definition: {{$DateTime.Now}}   {{$new Random().Next(100,200)}}
        var pattern = @"{{(\$.*?)}}";
        var matches = Regex.Matches(_payload, pattern);
        foreach (Match m in matches)
        {
            if (!Expressions.ContainsKey(m.Value))
            {
                Expressions.Add(m.Value, m.Value.Substring(3, m.Value.Length - 5));
            }
        }
    }

    public async Task<string> GetPayload(int msgIndex)
    {
        Random rnd = new Random();
        string payload = _payload;

        // Evaluating Variables
        foreach (VariableDefinition v in Variables)
        {
            payload = payload.Replace("{{" + v.Name + "}}", v.Evaluate(msgIndex, rnd));
        }

        // Evaluating Expressions
        // https://github.com/dotnet/roslyn/blob/main/docs/wiki/Scripting-API-Samples.md
        foreach (string key in Expressions.Keys)
        {
            String exp = Expressions[key];

            if (!preCompiledScripts.TryGetValue(exp, out Script<Object> script))
            {
                script = CSharpScript.Create(exp, ScriptOptions.Default.WithImports("System"), typeof(ScriptFunctions));
                preCompiledScripts.TryAdd(exp, script);
            }

            var result = await script.RunAsync(_functions);

            payload = payload.Replace(key, result.ReturnValue.ToString());
        }

        return payload;
    }
}
