using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalApp
{
  internal class Environment : IEnvironmentResolver
  {
    private ConcurrentDictionary<string, object?> _variables;
    private Dictionary<string, IEnvironmentResolver> _namespaces;

    public Environment()
    {
      this._variables = new();
      this._namespaces = new();
    }

    public void Set(string path, object? value)
    {
      var prefixEnd = path.IndexOf(':');
      if (prefixEnd > 0)
      {
        var prefix = path.Substring(0, prefixEnd).ToLowerInvariant();
        var subPath = path.Substring(prefixEnd + 1);
        if (!this._namespaces.TryGetValue(prefix, out IEnvironmentResolver? resolver))
          throw new ArgumentException($"Invalid environment namespace {prefix}");
        resolver.Set(subPath, value);
      }
      this._variables.AddOrUpdate(path.ToLowerInvariant(), value, (_, _) => value);
    }

    public object? Resolve(string path)
    {
      var prefixEnd = path.IndexOf(':');
      if (prefixEnd > 0)
      {
        var prefix = path.Substring(0, prefixEnd).ToLowerInvariant();
        var subPath = path.Substring(prefixEnd + 1);
        if (!this._namespaces.TryGetValue(prefix, out IEnvironmentResolver? resolver))
          throw new ArgumentException($"Invalid environment namespace {prefix}");
        return resolver.Resolve(subPath);
      }
      this._variables.TryGetValue(path.ToLowerInvariant(), out object? value);
      return value;
    }

    public void RegisterNamespaceResolver(string @namespace, IEnvironmentResolver resolver)
    {
      if (this._namespaces.ContainsKey(@namespace))
        throw new InvalidOperationException($"Environment already contains prefix '{@namespace}'");
      if (string.IsNullOrWhiteSpace(@namespace))
        throw new ArgumentException("Invalid namespace");

      this._namespaces.Add(@namespace.ToLowerInvariant(), resolver);
    }
  }

  public interface IEnvironmentResolver
  {
    void Set(string path, object? value);
    object? Resolve(string path);
  }
}
