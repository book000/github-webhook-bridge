using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitHubWebhookBridge.Actions;
using Octokit.Webhooks;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// 実装済みアクションのペイロード型スキーマが変化したことを検知するスナップショットテスト。
/// Renovate による Octokit.Webhooks 更新後にこのテストが落ちた場合:
///   UPDATE_SNAPSHOTS=1 dotnet test --filter OctokitPayloadSchemaSnapshotTests
///   を実行してスナップショットを更新し、差分をレビューしてからコミットすること。
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
            $"Octokit.Webhooks モデルスキーマが変化しました。" +
            $"UPDATE_SNAPSHOTS=1 dotnet test --filter OctokitPayloadSchemaSnapshotTests を実行して差分を確認してください。");
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
                // Enum: メンバー名と基底値をスナップショット化する（追加・削除・変更を検知）
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
                     && !(propType.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(propType))
                     && propType.Namespace?.StartsWith("Octokit", StringComparison.Ordinal) == true)
            {
                // ネストした Octokit 型: 再帰的にスキーマを構築する
                props[jsonName] = BuildTypeSchema(propType, new HashSet<Type>(visited));
            }
            else if (propType.IsGenericType
                     && typeof(System.Collections.IEnumerable).IsAssignableFrom(propType))
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
