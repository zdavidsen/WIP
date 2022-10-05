using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalApp
{
  public interface ICommand
  {
    Task Execute(string originalInput, object?[] args, IEnvironmentResolver env, ITerminal terminal);
  }

  [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
  public class CommandAttribute : Attribute
  {
    public string Name { get; }

    public CommandAttribute(string name)
    {
      this.Name = name;
    }
  }

  [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
  public class AliasAttribute : Attribute
  {
    public string Name { get; }

    public AliasAttribute(string name)
    {
      this.Name = name;
    }
  }

  public class Command
  {
  }
}
