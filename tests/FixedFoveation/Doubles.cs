namespace BepInEx.Configuration;

public sealed class ConfigFile
{
    private readonly Dictionary<(string section, string key), object> _entries = new();
    public readonly List<(string section, string key)> BoundKeys = new();

    public void Seed<T>(string section, string key, T value) => _entries[(section, key)] = new ConfigEntry<T>(value);

    public ConfigEntry<T> Bind<T>(string section, string key, T value, string description)
    {
        BoundKeys.Add((section, key));
        if (_entries.TryGetValue((section, key), out var existing))
            return (ConfigEntry<T>)existing;
        var entry = new ConfigEntry<T>(value);
        _entries.Add((section, key), entry);
        return entry;
    }
}

public sealed class ConfigEntry<T>(T value)
{
    public T Value { get; set; } = value;
}
