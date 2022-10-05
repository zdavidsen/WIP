using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using TerminalApp.Commands;

namespace TerminalApp
{
  public class Application
  {
    public IEnvironmentResolver Environment => this._env;

    public string Prompt
    {
      get => GetVar("Prompt")?.ToString() ?? "> ";
      set => SetVar("Prompt", value);
    }

    internal readonly Environment _env;
    internal readonly Dictionary<string, CommandWrapper> _commands;

    private readonly ITerminal _realOutput;
    private bool _running;

    public Application(string prompt = "> ")
    {
      this._env = new Environment();
      this._commands = new Dictionary<string, CommandWrapper>();
      this._realOutput = new RealTerminal(this);

      this.Prompt = prompt;
      this._currentPrompt = prompt;

      this._commands.Add("help", new InternalCommandWrapper(new HelpCommand(this)));
      this._commands.Add("exit", new InternalCommandWrapper(new ExitCommand(this)));
      RegisterCommand(typeof(EchoCommand));
      RegisterCommand(typeof(SetVarCommand));
    }

    public void RegisterCommand(Type commandType)
    {
      var wrap = new CommandWrapper(commandType);
      if (!wrap.IsValid)
        throw new Exception($"Type '{commandType.FullName}' is not a valid command");
      // explicitly overwrite any alias that may have the same name?
      // TODO: reconsider concept of how command mapping and aliasing will work
      this._commands[wrap.Name] = wrap;
    }

    public void RegisterEnvironmentNamespace(string @namespace, IEnvironmentResolver resolver)
    {
      this._env.RegisterNamespaceResolver(@namespace, resolver);
    }

    public object? GetVar(string var)
    {
      return this._env.Resolve(var);
    }

    public void SetVar(string var, object? value)
    {
      this._env.Set(var, value);
    }

    // TODO
    //public async Task<> Evaluate(string input)
    //{
    //  var output = new FakeTerminal();
    //  try
    //  {
    //    await InternalEvaluate(input, output);
    //  }
    //  catch (Exception e)
    //  {
    //    output.WriteError(e);
    //  }
    //  return output.Stuff;
    //}

    public async Task Run()
    {
      this._running = true;
      while (this._running)
      {
        try
        {
          // TODO
          //this._currentPrompt = InternalEvaluate("$Prompt", this._realOutput)?.ToString() ?? "> ";
          var prompt = ResolveToken("$Prompt").ToString();
          if (string.IsNullOrEmpty(prompt))
            prompt = "> ";
          this._currentPrompt = prompt;
          Console.Write(this._currentPrompt);
          var input = await ProcessInput();
          //foreach (var token in tokens)
          //  Console.WriteLine(token);
          await InternalEvaluate(input, this._realOutput);
        }
        catch (Exception e)
        {
          WriteError("---Unhandled Exception---");
          WriteError(e.ToString());
          WriteError("---Unhandled Exception---");
        }
      }
    }

    public void Stop()
    {
      this._running = false;
    }

    private Task InternalEvaluate(string input, ITerminal output)
    {
      // TODO: multiline support
      var tokens = Tokenize(input);
      if (tokens.Count == 0)
        return Task.CompletedTask;
      // TODO: parse into an AST? and also respect parentheses?
      var evals = tokens.Select(t => ResolveToken(t));
      var first = evals.First();
      if (first.WasResolved)
      {
        output.WriteObject(first.Resolved);
        return Task.CompletedTask;
      }
      else if (this._commands.ContainsKey(first.Original))
      {
        var cmd = this._commands[first.Original];
        return 
          cmd.NewInstance()
            .Execute(input, evals.Select(e => e.GetValue()).ToArray(), this.Environment, output);
      }
      else
      {
        throw new Exception($"Unable to resolve input '{tokens[0]}'");
      }
    }

    private ResolvedToken ResolveToken(string token)
    {
      if (token[0] == '$')
      {
        return new ResolvedToken(token, true, this.Environment.Resolve(token.Trim().TrimStart('$')));
      }
      else if (token[0] == '"')
      {
        return new ResolvedToken(token, true, token.Trim('"'));
      }
      //else if (this._commands.TryGetValue(token, out var wrapper))
      //{
      //  return wrapper;
      //}
      // TODO: try to resolve numbers?
      return new ResolvedToken(token, false, null);
    }

    // TODO: this is gonna require a whole AST isn't it...
    //private readonly Dictionary<char, char> _groupingConstructs =
    //  new Dictionary<char, char>
    //  {
    //    ['"'] = '"',
    //    ['('] = ')',
    //  };

    private List<string> Tokenize(string input)
    {
      var args = new List<string>();
      var processedUpTo = 0;
      while (processedUpTo < input.Length)
      {
        // TODO: escape character?
        var nextQuote = input.IndexOf('"', processedUpTo);
        if (nextQuote > processedUpTo || nextQuote < 0)
        {
          var end = nextQuote;
          if (end < 0)
            end = input.Length;
          var tokens = input.Substring(processedUpTo, end - processedUpTo).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
          args.AddRange(tokens);
          //foreach (var token in tokens)
          //  args.Add(EvaluateToken(token));
          processedUpTo = nextQuote;
          if (nextQuote < 0)
            break;
        }
        var closingQuote = input.IndexOf('"', nextQuote + 1);
        if (closingQuote < 0)
          throw new Exception($"Unmatched '{"\""}' detected in input!");
        args.Add(input.Substring(nextQuote, closingQuote - nextQuote + 1));
        processedUpTo = closingQuote + 1;
      }
      return args;
    }


    internal void WriteObject(object? obj)
    {
      // TODO: write this to a background queue so that it doesn't have to wait for a lock
      //  but is still synchronous
      this._consoleLock.Wait();
      try
      {
        WriteOutput(obj?.ToString());
      }
      finally
      {
        this._consoleLock.Release();
      }
    }

    internal void WriteError(string error)
    {
      // TODO: write this to a background queue so that it doesn't have to wait for a lock
      //  but is still synchronous
      this._consoleLock.Wait();
      try
      {
        var foreground = Console.ForegroundColor;
      Console.ForegroundColor = ConsoleColor.Red;
      WriteOutput(error);
      Console.ForegroundColor = foreground;
      }
      finally
      {
        this._consoleLock.Release();
      }
    }

    internal void WriteError(Exception e)
    {
      WriteError(e.ToString());
    }


    private string _currentPrompt;
    private int _inputTop;
    // TODO: make this an... editor tree?
    private List<char> _input = new(256);
    private int _inputIndex;
    private List<string> _history = new();
    private int _historyIndex;

    private async Task<string> ProcessInput()
    {
      this._inputTop = Console.CursorTop;
      this._input.Clear();
      this._inputIndex = 0;
      this._historyIndex = this._history.Count;
      while (true)
      {
        ConsoleKeyInfo key = Console.ReadKey(true);
        await this._consoleLock.WaitAsync();
        try
        {
          switch (key)
          {
            case { Key: ConsoleKey.LeftArrow }:
              if (--this._inputIndex < 0)
                this._inputIndex = 0;
              UpdateCursorPos();
              break;

            case { Key: ConsoleKey.RightArrow }:
              if (++this._inputIndex >= this._input.Count)
                this._inputIndex = this._input.Count;
              UpdateCursorPos();
              break;

            case { Key: ConsoleKey.UpArrow }:
              if (--this._historyIndex < 0)
              {
                this._historyIndex = 0;
                continue;
              }
              EraseInput();
              this._input.Clear();
              this._input.AddRange(this._history[this._historyIndex]);
              this._inputIndex = this._input.Count;
              RewriteInputFrom(0);
              break;

            case { Key: ConsoleKey.DownArrow }:
              if (++this._historyIndex >= this._history.Count)
              {
                this._historyIndex = this._history.Count - 1;
                continue;
              }
              EraseInput();
              this._input.Clear();
              this._input.AddRange(this._history[this._historyIndex]);
              this._inputIndex = this._input.Count;
              RewriteInputFrom(0);
              break;

            case { Key: ConsoleKey.Escape }:
              EraseInput();
              this._input.Clear();
              this._inputIndex = 0;
              UpdateCursorPos();
              break;

            case { Key: ConsoleKey.Backspace }:
              if (this._inputIndex <= 0)
                break;
              RemoveCharAt(--this._inputIndex);
              break;

            case { Key: ConsoleKey.Delete }:
              if (this._inputIndex >= this._input.Count)
                break;
              RemoveCharAt(this._inputIndex);
              break;

            case { Key: ConsoleKey.Enter }:
              WriteInput(System.Environment.NewLine);
              var input = string.Join("", this._input);
              if (this._history.Count == 0 || this._history.Last() != input)
              {
                if (this._history.Count >= 100)
                  this._history.RemoveAt(0);
                this._history.Add(input);
              }
              this._input.Clear();
              this._inputIndex = 0;
              this._currentPrompt = "";
              this._inputTop = Console.CursorTop;
              return input;

            default:
              if (key.KeyChar != 0)
                this._input.Insert(this._inputIndex++, key.KeyChar);
              WriteInput(key.KeyChar);
              RewriteInputFrom(this._inputIndex);
              break;
          }
        }
        finally
        {
          this._consoleLock.Release();
        }
      }
    }


    private SemaphoreSlim _consoleLock = new SemaphoreSlim(1);

    [Conditional("DEBUG")]
    private void CheckLock([CallerMemberName] string caller = "")
    {
      if (this._consoleLock.CurrentCount != 0)
        throw new InvalidOperationException($"_consoleLock must be locked before calling {caller}");
    }

    private void WriteInput(string input)
    {
      CheckLock();
      Console.Write(input);
    }

    private void WriteInput(char input)
    {
      CheckLock();
      Console.Write(input);
    }

    private void WriteOutput(object? output)
    {
      CheckLock();
      ErasePromptAndInput();
      Console.Write(output?.ToString());
      Console.WriteLine();
      this._inputTop = Console.CursorTop;
      Console.Write(this._currentPrompt);
      RewriteInputFrom(0);
    }

    private void UpdateCursorPos(int? index = null)
    {
      CheckLock();
      if (index == null)
        index = this._inputIndex;
      var len = this._currentPrompt.Length + index.Value;
      Console.SetCursorPosition(len % Console.BufferWidth, this._inputTop + len / Console.BufferWidth);
    }

    private void RemoveCharAt(int index)
    {
      CheckLock();
      var printedLength = this._currentPrompt.Length + this._input.Count - 1;
      this._input.RemoveAt(index);
      Console.SetCursorPosition(printedLength % Console.BufferWidth, this._inputTop + printedLength / Console.BufferWidth);
      // this is only needed if deleting the last character
      Console.Write(' ');
      RewriteInputFrom(index);
    }

    private void RewriteInputFrom(int index)
    {
      CheckLock();
      UpdateCursorPos(index);
      // TODO: optimize this... like a lot
      for (int i = index; i < this._input.Count; i++)
        Console.Write(this._input[i]);
      UpdateCursorPos();
    }

    private void ErasePromptAndInput()
    {
      CheckLock();
      // TODO: optimize this... like a lot
      Console.SetCursorPosition(0, this._inputTop);
      for (int i = 0; i < this._currentPrompt.Length + this._input.Count; i++)
        Console.Write(' ');
    }

    private void EraseInput()
    {
      CheckLock();
      // TODO: optimize this... like a lot
      Console.SetCursorPosition(this._currentPrompt.Length, this._inputTop);
      for (int i = 0; i < this._input.Count; i++)
        Console.Write(' ');
    }




    private struct ResolvedToken 
    {
      public string Original { get; }
      public bool WasResolved { get; }
      public object? Resolved { get; }
      public ResolvedToken(string original, bool wasResolved, object? value) 
      { 
        this.Original = original;
        this.WasResolved = wasResolved;
        this.Resolved = value; 
      }

      public object? GetValue()
        => WasResolved ? Resolved : Original;

      public override string ToString()
      {
        return GetValue()?.ToString() ?? "";
      }
    }


    internal class CommandWrapper
    {
      public string Name { get; }

      public bool IsValid { get; }

      // probably keep this around for bookkeeping/equality checks?
      private readonly Type _commandType;
      private readonly ConstructorInfo? _constructor;

      public CommandWrapper(Type commandType)
      {
        this.Name = "";
        this.IsValid = false;
        this._commandType = commandType;
        if (!commandType.IsAssignableTo(typeof(ICommand)))
          return;
        var attr = this._commandType.GetCustomAttribute<CommandAttribute>();
        if (attr is null)
          return;
        this.Name = attr.Name;
        this._constructor = 
          this._commandType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, new Type[] { })!;
        if (this._constructor is null)
          return;
        this.IsValid = true;
      }

      public virtual ICommand NewInstance()
      {
        if (!this.IsValid)
          throw new Exception($"Type '{this._commandType.FullName}' is not a valid command");
        return (ICommand)this._constructor!.Invoke(new object[] { });
      }
    }


    internal class InternalCommandWrapper : CommandWrapper
    {
      private readonly ICommand _instance;

      public InternalCommandWrapper(ICommand instance)
        : base(instance.GetType())
      {
        this._instance = instance;
      }

      public override ICommand NewInstance()
      {
        return this._instance;
      }
    }


    private class RealTerminal : ITerminal
    {
      private Application _app;

      public RealTerminal(Application app)
      {
        this._app = app;
      }

      public void WriteObject(object? obj)
      {
        this._app.WriteObject(obj);
      }

      public void WriteError(Exception e)
      {
        this._app.WriteError(e);
      }
    }

    //private class FakeTerminal : ITerminal
    //{
    //  public void WriteObject(object obj)
    //  {
    //    throw new NotImplementedException();
    //  }

    //  public void WriteError(Exception e)
    //  {
    //    throw new NotImplementedException();
    //  }
    //}
  }

  public interface ITerminal
  {
    public void WriteObject(object? obj);

    public void WriteError(Exception e);
  }
}
