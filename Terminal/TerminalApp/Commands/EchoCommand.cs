using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalApp.Commands
{
  [Command("echo")]
  internal class EchoCommand : ICommand
  {
    public Task Execute(string originalInput, object[] args, IEnvironmentResolver env, ITerminal terminal)
    {
      terminal.WriteObject(string.Join(" ", args.Skip(1)));
      return Task.CompletedTask;
    }
  }
}
