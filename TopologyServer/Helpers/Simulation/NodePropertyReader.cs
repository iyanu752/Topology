using System.Text.Json;

namespace TopologyServer;

public static class NodePropertyReader
{
    public static int GetInt(Node node, string key, int defaultValue)
    {
        if (!node.Properties.TryGetValue(key, out var value) || value is null)
        {
            return defaultValue;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            double doubleValue => (int)doubleValue,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetInt32(out var intValue) => intValue,
            string stringValue when int.TryParse(stringValue, out var intValue) => intValue,
            _ => defaultValue
        };
    }

    public static bool GetBool(Node node, string key, bool defaultValue)
    {
        if (!node.Properties.TryGetValue(key, out var value) || value is null)
        {
            return defaultValue;
        }

        return value switch
        {
            bool boolValue => boolValue,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.True => true,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.False => false,
            string stringValue when bool.TryParse(stringValue, out var boolValue) => boolValue,
            _ => defaultValue
        };
    }
}
