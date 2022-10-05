using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalApp.Commands
{
  [Command("set-var")]
  internal class SetVarCommand : ICommand
  {
    public Task Execute(string originalInput, object?[] args, IEnvironmentResolver env, ITerminal terminal)
    {
      if (args.Length < 3)
        throw new Exception("Requires a variable name and a variable value as arguments");

      var path = args[1]?.ToString();
      if (string.IsNullOrEmpty(path))
        throw new Exception("First argument must resolve to a non-empty string");

      env.Set(path, args[2]);
      return Task.CompletedTask;
    }
  }
}
