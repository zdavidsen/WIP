using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalApp.Commands
{
  [Command("help")]
  internal class HelpCommand : ICommand
  {
    private readonly Application _app;

    public HelpCommand(Application app)
    {
      this._app = app;
    }

    public Task Execute(string originalInput, object[] args, IEnvironmentResolver env, ITerminal terminal)
    {
      terminal.WriteObject("Available commands:");
      // filter out aliases
      foreach (var cmd in this._app._commands.Where(c => c.Key == c.Value.Name))
      {
        terminal.WriteObject($"\t{cmd.Key}");
      }
      return Task.CompletedTask;
    }
  }
}
