using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalApp.Commands
{
  [Command("exit")]
  internal class ExitCommand : ICommand
  {
    private readonly Application _app;

    public ExitCommand(Application app)
    {
      this._app = app;
    }

    public Task Execute(string originalInput, object[] args, IEnvironmentResolver env, ITerminal terminal)
    {
      this._app.Stop();
      return Task.CompletedTask;
    }
  }
}
