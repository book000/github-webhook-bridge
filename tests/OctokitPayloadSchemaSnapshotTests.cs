using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitHubWebhookBridge.Actions;
using Octokit.Webhooks;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// Snapshot test that detects changes in the payload type schemas of implemented actions.
/// If this test fails after a Renovate-driven Octokit.Webhooks update:
///   Run UPDATE_SNAPSHOTS=1 dotnet test --filter OctokitPayloadSchemaSnapshotTests
///   to update the snapshot, then review the diff before committing.
/// </summary>
public class OctokitPayloadSchemaSnapshotTests
{
    private static readonly string SnapshotPath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "Snapshots",
        "octokit-payload-schema.json");

    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };

    [Fact]
    public void ImplementedPayloadTypes_MustMatchSnapshot()
    {
        var schema = BuildSchema();
        var actual = JsonSerializer.Serialize(schema, PrettyOptions);

        if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath)!);
            File.WriteAllText(SnapshotPath, actual);
            return;
        }

        Assert.True(File.Exists(SnapshotPath),
            $"Snapshot file not found at {SnapshotPath}. " +
            $"Run: UPDATE_SNAPSHOTS=1 dotnet test --filter OctokitPayloadSchemaSnapshotTests");

        var expected = File.ReadAllText(SnapshotPath);
        Assert.True(expected == actual,
            $"The Octokit.Webhooks model schema has changed. " +
            $"Run UPDATE_SNAPSHOTS=1 dotnet test --filter OctokitPayloadSchemaSnapshotTests to inspect the diff.");
    }

    private static SortedDictionary<string, object> BuildSchema()
    {
        var payloadTypes = typeof(GitHubEventAttribute).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<GitHubEventAttribute>() != null && !t.IsAbstract)
            .Select(GetPayloadType)
            .Distinct()
            .OrderBy(t => t.Name);

        var schema = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var type in payloadTypes)
            schema[type.Name] = BuildTypeSchema(type, new HashSet<Type>());

        return schema;
    }

    private static object BuildTypeSchema(Type type, HashSet<Type> visited)
    {
        if (!visited.Add(type))
            return "«circular»";

        var props = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                 .OrderBy(p => p.Name))
        {
            var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name;
            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (propType.IsEnum)
            {
                // Enum: snapshot the member names and underlying values (detects additions, removals, and changes)
                props[jsonName] = new
                {
                    type = "enum:" + propType.Name,
                    members = Enum.GetNames(propType)
                                  .Zip(Enum.GetValues(propType).Cast<int>(),
                                       (n, v) => $"{n}={v}")
                                  .OrderBy(s => s)
                                  .ToArray(),
                };
            }
            else if (propType.IsClass
                     && propType != typeof(string)
                     && propType != typeof(Uri)
                     && !propType.IsArray
                     && !(propType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(propType))
                     && propType.Namespace?.StartsWith("Octokit", StringComparison.Ordinal) == true)
            {
                // Nested Octokit type: build the schema recursively
                props[jsonName] = BuildTypeSchema(propType, new HashSet<Type>(visited));
            }
            else if (propType.IsGenericType
                     && typeof(IEnumerable).IsAssignableFrom(propType))
            {
                var elemType = propType.GetGenericArguments().FirstOrDefault();
                props[jsonName] = "array:" + (elemType?.Name ?? "object");
            }
            else if (propType.IsArray)
            {
                props[jsonName] = "array:" + (propType.GetElementType()?.Name ?? "object");
            }
            else
            {
                props[jsonName] = propType.Name;
            }
        }

        return props;
    }

    private static Type GetPayloadType(Type actionType)
    {
        var b = actionType.BaseType;
        while (b != null && !(b.IsGenericType && b.GetGenericTypeDefinition() == typeof(BaseAction<>)))
            b = b.BaseType;
        return b?.GetGenericArguments()[0]
               ?? throw new InvalidOperationException($"Cannot find payload type for {actionType.Name}");
    }
}
