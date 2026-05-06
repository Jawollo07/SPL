using Avalonia.Controls;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Jugend_Forscht.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // --- Daten Strukturen ---
    public enum TokenType { Keyword, Identifier, Number, String, Operator, Separator, EOF }
    public abstract class Statement { }
    
    public class Drucken : Statement { public string Value { get; set; } }
    public class Zuweisung : Statement { public string VariableName { get; set; } public string Wert { get; set; } }
    
    public record Token(TokenType Type, string Value);

    // --- Logik ---
    public void ExecuteCode()
    {
        try 
        {   
            // 1. Hole den Code aus dem Editor
            string input = Editor.Text ?? ""; 

            // 2. Tokenize
            var tokenizer = new Tokenizer();
            var tokens = tokenizer.Tokenize(input);

            // 3. Parsen
            var parser = new Parser(tokens);
            var statements = parser.Parse();

            // 4. Starte
            RunInterpreter(statements);
        }
        catch (System.Exception ex)
        {
            Output.Text += $"Fehler: {ex.Message}\n";
        }
    }

    public class Tokenizer
    {
        private readonly List<(TokenType Type, string Pattern)> _definitions = new()
        {
            (TokenType.Keyword, @"\b(drucke|ist|sonst|während)\b"),
            (TokenType.Number, @"\d+"),
            (TokenType.Identifier, @"[a-zA-Z_][a-zA-Z0-9_]*"),
            (TokenType.Operator, @"[\+\-\*\/=]"),
            (TokenType.Separator, @"[\(\);]")
        };

        public List<Token> Tokenize(string input)
        {
            var tokens = new List<Token>();
            int index = 0;
            while (index < input.Length)
            {
                if (char.IsWhiteSpace(input[index])) { index++; continue; }

                bool matched = false;
                foreach (var def in _definitions)
                {
                    var regex = new Regex("^" + def.Pattern);
                    var match = regex.Match(input.Substring(index));
                    if (match.Success)
                    {
                        tokens.Add(new Token(def.Type, match.Value));
                        index += match.Length;
                        matched = true;
                        break;
                    }
                }
                if (!matched) throw new System.Exception($"Unerwartetes Zeichen: {input[index]}");
            }
            tokens.Add(new Token(TokenType.EOF, string.Empty));
            return tokens;
        }
    }

    public class Parser
    {
        private readonly List<Token> _tokens;
        private int _position = 0;

        public Parser(List<Token> tokens) => _tokens = tokens;
        
        private Token Peek() => _tokens[_position];
        private Token Match(params TokenType[] types) 
        {
            foreach(var type in types)
            {
                if (Peek().Type == type) return _tokens[_position++];
            }
            throw new System.Exception($"Erwartet: {string.Join(" oder ", types)}, gefunden: {Peek().Type}");
        }

        public List<Statement> Parse()
        {
            var statements = new List<Statement>();
            while (Peek().Type != TokenType.EOF)
            {
                if (Peek().Type == TokenType.Keyword && Peek().Value == "drucke")
                {
                    Match(TokenType.Keyword);
                    // Can print a variable OR a number
                    var valToken = Match(TokenType.Identifier, TokenType.Number);
                    statements.Add(new Drucken { Value = valToken.Value });
                }
                else if (Peek().Type == TokenType.Identifier)
                {
                    var name = Match(TokenType.Identifier).Value;
                    Match(TokenType.Operator); // Expects "="
                    var val = Match(TokenType.Identifier, TokenType.Number).Value;
                    statements.Add(new Zuweisung { VariableName = name, Wert = val });
                }
                else { _position++; } // Skip unknown to avoid infinite loops
            }
            return statements;
        }
    }

    public void RunInterpreter(List<Statement> statements)
    {
        var variables = new Dictionary<string, string>();
        foreach (var smt in statements)
        {
            if (smt is Drucken druck)
            {
                // Logic: If it's a variable name, print the value. Otherwise print the literal.
                string toPrint = variables.ContainsKey(druck.Value) ? variables[druck.Value] : druck.Value;
                Output.Text += $"> {toPrint}\n";
            }
            else if (smt is Zuweisung zuweis)
            {
                variables[zuweis.VariableName] = zuweis.Wert;
            }
        }
    }
}