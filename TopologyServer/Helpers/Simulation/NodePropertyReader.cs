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
            decimal decimalValue => (int)decimalValue,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetInt32(out var intValue) => intValue,
            string stringValue when int.TryParse(stringValue, out var intValue) => intValue,
            _ => defaultValue
        };
    }

    public static int GetInt(Node node, string primaryKey, string fallbackKey, int defaultValue)
    {
        var primaryValue = GetInt(node, primaryKey, int.MinValue);
        return primaryValue == int.MinValue ? GetInt(node, fallbackKey, defaultValue) : primaryValue;
    }

    public static double GetDouble(Node node, string key, double defaultValue)
    {
        if (!node.Properties.TryGetValue(key, out var value) || value is null)
        {
            return defaultValue;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => longValue,
            double doubleValue => doubleValue,
            decimal decimalValue => (double)decimalValue,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetDouble(out var doubleValue) => doubleValue,
            string stringValue when double.TryParse(stringValue, out var doubleValue) => doubleValue,
            _ => defaultValue
        };
    }

    public static double GetDouble(Node node, string primaryKey, string fallbackKey, double defaultValue)
    {
        var primaryValue = GetDouble(node, primaryKey, double.NaN);
        return double.IsNaN(primaryValue) ? GetDouble(node, fallbackKey, defaultValue) : primaryValue;
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

    public static bool GetBool(Node node, string primaryKey, string fallbackKey, bool defaultValue)
    {
        if (HasProperty(node, primaryKey))
        {
            return GetBool(node, primaryKey, defaultValue);
        }

        return GetBool(node, fallbackKey, defaultValue);
    }

    public static string? GetString(Node node, string key, string? defaultValue = null)
    {
        if (!node.Properties.TryGetValue(key, out var value) || value is null)
        {
            return defaultValue;
        }

        return value switch
        {
            string stringValue => stringValue,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String => jsonElement.GetString(),
            JsonElement jsonElement when jsonElement.ValueKind != JsonValueKind.Null => jsonElement.ToString(),
            _ => value.ToString()
        };
    }

    public static bool HasProperty(Node node, string key)
    {
        return node.Properties.TryGetValue(key, out var value) && value is not null;
    }
}