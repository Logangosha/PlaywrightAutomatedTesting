using System.Reflection;

// DISCOVERS ALL AVAILABLE TESTS IN THE ASSEMBLY THAT CONTAINS TestBase, READING THEIR TRAITS.
public class Discovery : IDiscovery
{
    public IReadOnlyList<DiscoveredTest> Discover()
    {
        var tests = new List<DiscoveredTest>();
        var assembly = typeof(TestBase).Assembly;

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract)
                continue;

            var classTraits = ReadTraits(type).ToList();
            var site = FirstValue(classTraits, "Site") ?? "";
            var kind = FirstValue(classTraits, "Kind") ?? "";
            var module = FirstValue(classTraits, "Module");      
            var envs = classTraits.Where(t => t.Name == "Env")
                                  .Select(t => t.Value)
                                  .ToList();

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!IsTestMethod(method))
                    continue;

                var category = FirstValue(ReadTraits(method), "Category");

                tests.Add(new DiscoveredTest
                {
                    FullyQualifiedName = Fqn(type, method),
                    Method = method.Name,
                    Site = site,
                    Envs = envs,
                    Kind = kind,
                    Module = module,
                    Category = category
                });
            }
        }

        return tests
            .OrderBy(t => t.Site)
            .ThenBy(t => t.Kind)
            .ThenBy(t => t.Module)
            .ThenBy(t => t.Method)
            .ToList();
    }
    private static bool IsTestMethod(MethodInfo method) =>
        method.GetCustomAttributesData()
              .Any(d => typeof(FactAttribute).IsAssignableFrom(d.AttributeType));
    private static IEnumerable<(string Name, string Value)> ReadTraits(MemberInfo member)
    {
        foreach (var data in member.GetCustomAttributesData())
        {
            if (data.AttributeType != typeof(TraitAttribute))
                continue;

            var args = data.ConstructorArguments;
            if (args.Count != 2)
                continue;

            var name = args[0].Value?.ToString();
            var value = args[1].Value?.ToString();
            if (!string.IsNullOrEmpty(name) && value is not null)
                yield return (name, value);
        }
    }

    private static string? FirstValue(IEnumerable<(string Name, string Value)> traits, string name)
    {
        foreach (var t in traits)
            if (t.Name == name)
                return t.Value;
        return null;
    }
    private static string Fqn(Type type, MethodInfo method) =>
        $"{type.FullName!.Replace('+', '.')}.{method.Name}";
}
