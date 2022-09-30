using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using TerminalApp.Commands;

namespace TerminalApp
{
  public class Application
  {
    public IEnvironmentResolver Environment => this._env;

    internal readonly Environment _env;
    internal readonly Dictionary<string, CommandWrapper> _commands;
    private readonly ITerminal _realOutput;

    public Application(string prompt = "> ")
    {
      this._env = new Environment();
      this._commands = new Dictionary<string, CommandWrapper>();
      this._realOutput = new RealTerminal(this);

      SetPrompt(prompt);
      this._currentPrompt = prompt;

      this._commands.Add("help", new InternalCommandWrapper(new HelpCommand(this)));
    }

    public void RegisterEnvironmentNamespace(string @namespace, IEnvironmentResolver resolver)
    {
      this._env.RegisterNamespaceResolver(@namespace, resolver);
    }

    public void SetPrompt(string prompt)
    {
      this.Environment.Set("Prompt", prompt);
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
      while (true)
      {
        try
        {
          // TODO
          //this._currentPrompt = InternalEvaluate("$Prompt", this._realOutput)?.ToString() ?? "> ";
          this._currentPrompt = ResolveToken("$Prompt")?.ToString() ?? "> ";
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

    private Task InternalEvaluate(string input, ITerminal output)
    {
      // TODO: multiline support
      var tokens = Tokenize(input);
      if (tokens.Count == 0)
        return Task.CompletedTask;
      // TODO: parse into an AST? and also respect parentheses?
      var first = ResolveToken(tokens[0]);
      if (first is CommandWrapper cmd)
      {
        return cmd.NewInstance().Execute(input, tokens.ToArray(), this.Environment, output);
      }
      else if (first is ResolvedToken resolved)
      {
        output.WriteObject(resolved.Resolved);
        return Task.CompletedTask;
      }
      else
      {
        throw new Exception($"Unable to resolve input '{tokens[0]}'");
      }
    }

    private object? ResolveToken(string token)
    {
      if (token[0] == '$')
      {
        return new ResolvedToken(this.Environment.Resolve(token.Trim().TrimStart('$')));
      }
      else if (token[0] == '"')
      {
        return new ResolvedToken(token.Trim('"'));
      }
      else if (this._commands.TryGetValue(token, out var wrapper))
      {
        return wrapper;
      }
      // TODO: try to resolve numbers?
      return token;
    }

    // TODO: this is gonna require a whole tree structure isn't it...
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
      Console.WriteLine(obj?.ToString());
    }

    internal void WriteError(string error)
    {
      var foreground = Console.ForegroundColor;
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine(error);
      Console.ForegroundColor = foreground;
    }

    internal void WriteError(Exception e)
    {
      var foreground = Console.ForegroundColor;
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine(e.ToString());
      Console.ForegroundColor = foreground;
    }


    private string _currentPrompt;
    private int _inputTop;
    // TODO: make this an... editor tree?
    private List<char> _input = new(256);
    private int _inputIndex;

    private async Task<string> ProcessInput()
    {
      this._inputTop = Console.CursorTop;
      while (true)
      {
        ConsoleKeyInfo key = Console.ReadKey(true);
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

          case { Key: ConsoleKey.Escape }:
            EraseInputFromTerminal();
            Console.SetCursorPosition(0, this._inputTop);
            Console.Write(this._currentPrompt);
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
            Console.WriteLine();
            var input = string.Join("", this._input);
            this._input.Clear();
            this._inputIndex = 0;
            return input;

          default:
            if (key.KeyChar != 0)
              this._input.Insert(this._inputIndex++, key.KeyChar);
            Console.Write(key.KeyChar);
            RewriteInputFrom(this._inputIndex);
            break;
        }
      }
    }

    private void UpdateCursorPos()
    {
      Console.CursorLeft = this._currentPrompt.Length + this._inputIndex;
    }

    private void RemoveCharAt(int index)
    {
      var printedLength = this._currentPrompt.Length + this._input.Count - 1;
      this._input.RemoveAt(index);
      Console.SetCursorPosition(printedLength % Console.BufferWidth, this._inputTop + printedLength / Console.BufferWidth);
      Console.Write(' ');
      UpdateCursorPos();
      RewriteInputFrom(index);
    }

    private void RewriteInputFrom(int index)
    {
      // TODO: optimize this... like a lot
      for (int i = index; i < this._input.Count; i++)
        Console.Write(this._input[i]);
      UpdateCursorPos();
    }

    private void EraseInputFromTerminal()
    {
      Console.CursorLeft = 0;
      for (int i = 0; i < this._currentPrompt.Length + this._input.Count; i++)
        Console.Write(' ');
    }




    private struct ResolvedToken 
    { 
      public object? Resolved { get; }
      public ResolvedToken(object? resolved) { this.Resolved = resolved; }

      public override string ToString()
      {
        return Resolved?.ToString() ?? "";
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
