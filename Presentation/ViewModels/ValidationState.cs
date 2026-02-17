using System.Collections.Generic;
using System.Linq;

namespace WebLoadTester.Presentation.ViewModels;

public sealed class ValidationState
{
    private readonly Dictionary<string, string> _errorsByKey = new();

    public HashSet<string> TouchedKeys { get; } = new();

    public bool ShowAllErrors { get; private set; }

    public IReadOnlyDictionary<string, string> ErrorsByKey => _errorsByKey;

    public void MarkTouched(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            TouchedKeys.Add(key);
        }
    }

    public void MarkAllTouched(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            MarkTouched(key);
        }
    }

    public void ShowAll()
    {
        ShowAllErrors = true;
        MarkAllTouched(_errorsByKey.Keys);
    }

    public void ResetVisibility()
    {
        ShowAllErrors = false;
        TouchedKeys.Clear();
    }

    public void SetErrors(IDictionary<string, string> errors)
    {
        _errorsByKey.Clear();
        foreach (var kv in errors.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)))
        {
            _errorsByKey[kv.Key] = kv.Value;
        }
    }

    public string GetError(string key)
    {
        return _errorsByKey.TryGetValue(key, out var value) ? value : string.Empty;
    }

    public bool IsVisible(string key)
    {
        return ShowAllErrors || TouchedKeys.Contains(key);
    }

    public string GetVisibleError(string key)
    {
        return IsVisible(key) ? GetError(key) : string.Empty;
    }

    public bool HasVisibleError(string key)
    {
        return !string.IsNullOrWhiteSpace(GetVisibleError(key));
    }

    public bool HasErrors => _errorsByKey.Count > 0;

    public string? GetFirstVisibleErrorKey(IEnumerable<string> preferredOrder)
    {
        foreach (var key in preferredOrder)
        {
            if (HasVisibleError(key))
            {
                return key;
            }
        }

        foreach (var key in _errorsByKey.Keys)
        {
            if (HasVisibleError(key))
            {
                return key;
            }
        }

        return null;
    }
}
