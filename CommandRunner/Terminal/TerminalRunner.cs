using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandRunner.Terminal
{
    public class TerminalRunner : IStartableRunner {
        private readonly RunnerConfiguration _configuration;
        private TerminalState _state;
        public TerminalRunner (RunnerConfiguration configuration)
        {
            _configuration = configuration;
            _state = new TerminalState(_configuration);
            Console.Title = configuration.Title;
        }
        public void Start()
        {
            string input;
            Console.ForegroundColor = _configuration.TerminalColor;
            do
            {
                for (int i = 0; i < Console.WindowWidth; i++)
                {
                    Console.Write("-");
                }

                var menuItems = _state.Menu.OfType<NavigatableCommand>().OrderBy(x => x.Identifier);
                if (menuItems.Any())
                {
                    Console.WriteLine("Menu's available (type help x to print sub items):");
                    foreach (ICommand command in _state.Menu.OfType<NavigatableCommand>().OrderBy(x => x.Identifier))
                    {
                        Console.Write("  ");
                        command.WriteToConsole();
                    }
                    Console.WriteLine();
                }

                var commands = _state.Menu.OfType<SingleCommand>().OrderBy(x => x.Identifier);

                if (commands.Any())
                {
                    Console.WriteLine("Commands: ");
                    foreach (ICommand command in commands)
                    {
                        Console.Write("  ");
                        command.WriteToConsole();
                    }
                }
                
                Console.Write($"{Environment.NewLine}Command> ");
                input = Console.ReadLine() ?? string.Empty;

                var arguments = InputParser.ParseInputToArguments(input).ToList();
                Console.WriteLine();
                if (!arguments.Any())
                {
                    ConsoleWrite.WriteErrorLine("Please provide an argument");
                    continue;
                }
                if (arguments[0] == "help")
                {
                    var identifier = input.Split(' ')[1];
                    var item = menuItems.FirstOrDefault(x => x.Identifier == identifier);
                    if (item != null)
                    {
                        Console.WriteLine("MENU:");
                        item.WriteToConsole();
                        Console.WriteLine();
                        item.SubItems.ForEach(x => x.WriteToConsole());

                        Console.WriteLine();
                        Console.Write("Press enter to return to the menu");
                        Console.ReadLine();
                        continue;
                    }
                    else
                    {
                        ConsoleWrite.WriteErrorLine("Make sure you spelled the menu item correctly.");
                    }
                    continue;
                }

                var matches =
                    _state.Menu.Select(x => new {Key = x, Value = x.Match(arguments)})
                        .Where(x => x.Value != MatchState.Miss)
                        .ToDictionary(pair => pair.Key, pair => pair.Value);
                if (!matches.Any())
                {
                    ConsoleWrite.WriteErrorLine("Please provide a valid command.");
                    continue;
                }
                foreach (KeyValuePair<ICommand, MatchState> match in matches)
                {
                    if (match.Value == MatchState.MissingParameter)
                    {
                        ConsoleWrite.WriteErrorLine("Make sure you provide all the arguments for your command:");
                        match.Key.WriteToConsole();
                    }
                    else if (match.Value == MatchState.TooManyParameters)
                    {
                        ConsoleWrite.WriteErrorLine("Looks like you provided too much parameters for your command:");
                        match.Key.WriteToConsole();
                    }
                    else if (match.Value == MatchState.WrongTypes)
                    {
                        ConsoleWrite.WriteErrorLine("The provided types did not match the method parameters!");
                    }
                    else if (match.Value == MatchState.Matched)
                    {
                        ExecuteCommand(match.Key, arguments);
                    }
                }

            } while (string.IsNullOrEmpty(input) || !input.Equals("EXIT", StringComparison.OrdinalIgnoreCase));

            
            Console.ReadLine();
        }
        
        private void ExecuteCommand(ICommand command, List<string> arguments )
        {
            try
            {
                var commandInstance = _state.CommandActivator.Invoke(command.Type);
                Console.ForegroundColor = _configuration.CommandColor;
                if (command.Parameters.Count > 0)
                {
                    var typedParameters =
                        TypedParameterExecution.CreateTypedParameters(command.Parameters.ToArray(),
                            command.ArgumentsWithoutIdentifier(arguments));
                    
                    command.MethodInfo.Invoke(commandInstance, typedParameters);

                }
                else
                {
                    command.MethodInfo.Invoke(commandInstance, null);
                }
                var navigatableCommand = command as NavigatableCommand;
                if (navigatableCommand != null)
                {
                    _state.SetMenu(navigatableCommand.SubItems, navigatableCommand);
                }
            }
            catch (Exception exception)
            {
                ConsoleWrite.WriteErrorLine($"We couldn't setup your command parameters. Exception: {exception.Message}");
            }
            finally
            {
                Console.ForegroundColor = _configuration.TerminalColor;
            }
        }
    }

    public static class ConsoleWrite
    {
        public static void WriteErrorLine(string message)
        {
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = previousColor;
        }
    }
}