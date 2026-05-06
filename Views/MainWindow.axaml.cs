using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Jugend_Forscht.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    bool debugMode = true; // Debug-Modus für detailliertere Fehlermeldungen
    public void Run_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (debugMode)
        {
            Output.Text += "Starte Interpreter...\n";
        }
        ExecuteCode();
    }
    // --- Daten Strukturen ---
    public enum TokenType { Keyword, Identifier, Number, String, Operator, Separator, EOF }
    public record Token(TokenType Type, string Value);

    public abstract class Statement { }
    public abstract class Expression { }

    // Ausdrücke (Expressions)
    public class LiteralExpr : Expression { public object Value { get; set; } = 0; }
    public class VariableExpr : Expression { public string Name { get; set; } = ""; }
    public class BinaryExpr : Expression 
    { 
        public Expression Left { get; set; } = new LiteralExpr();
        public string Operator { get; set; } = "";
        public Expression Right { get; set; } = new LiteralExpr();
    }

    // Anweisungen (Statements)
    public class Drucken : Statement { public Expression ExpressionValue { get; set; } = new LiteralExpr(); }
    public class Zuweisung : Statement { public string VariableName { get; set; } = ""; public Expression Wert { get; set; } = new LiteralExpr(); }
    
    public class WennStatement : Statement
    {
        public Expression Bedingung { get; set; } = new LiteralExpr();
        public List<Statement> DannBlock { get; set; } = new();
        public List<Statement> SonstBlock { get; set; } = new();
    }

    // --- Logik ---
    private Dictionary<string, double> _variables = new();

    public void ExecuteCode()
    {
        try 
        {   
            _variables.Clear(); // Variablen bei jedem Start zurücksetzen
            string input = Editor.Text ?? ""; 

            var tokenizer = new Tokenizer();
            var tokens = tokenizer.Tokenize(input);

            var parser = new Parser(tokens);
            var statements = parser.Parse();

            RunInterpreter(statements);
        }
        catch (System.Exception ex)
        {
            Output.Text += $"Fehler: {ex.Message}\n";
        }
    }

    public double Evaluate(Expression expr)
    {
        return expr switch
        {
            LiteralExpr l => Convert.ToDouble(l.Value),
            VariableExpr v => _variables.ContainsKey(v.Name) ? _variables[v.Name] : throw new Exception($"Variable '{v.Name}' nicht definiert."),
            BinaryExpr b => b.Operator switch
            {
                "+" => Evaluate(b.Left) + Evaluate(b.Right),
                "-" => Evaluate(b.Left) - Evaluate(b.Right),
                "*" => Evaluate(b.Left) * Evaluate(b.Right),
                "/" => Evaluate(b.Left) / Evaluate(b.Right),
                // Vergleichsoperatoren
                ">" => Evaluate(b.Left) > Evaluate(b.Right) ? 1 : 0,
                "<" => Evaluate(b.Left) < Evaluate(b.Right) ? 1 : 0,
                "==" => Evaluate(b.Left) == Evaluate(b.Right) ? 1 : 0,
                _ => throw new Exception($"Unbekannter Operator: {b.Operator}")
            },
            _ => 0
        };
    }

    public void RunInterpreter(List<Statement> statements)
    {
        foreach (var smt in statements)
        {
            if (smt is Drucken druck)
            {
                double result = Evaluate(druck.ExpressionValue);
                Output.Text += $"> {result}\n";
            }
            else if (smt is Zuweisung zuweis)
            {
                _variables[zuweis.VariableName] = Evaluate(zuweis.Wert);
            }
        }
    }

    public class Tokenizer
    {
        private readonly List<(TokenType Type, string Pattern)> _definitions = new()
        {
            (TokenType.Keyword, @"\b(drucke|ist|sonst|während)\b"),
            (TokenType.Number, @"\d+"),
            (TokenType.Identifier, @"[a-zA-Z_][a-zA-Z0-9_]*"),
            (TokenType.Operator, @"(==|>=|<=|>|<|\+|-|\*|/|=)"), // Reihenfolge wichtig: == vor =
            (TokenType.Separator, @"[\(\);{}]"), // { } hinzugefügt
            (TokenType.String, "^\"[^\"]*\"") // Erkennt alles zwischen zwei Anführungszeichen
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

    // --- Hilfsmethoden ---
    private Token Peek() => _tokens[_position];

    private Token Match(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Peek().Type == type) return _tokens[_position++];
        }
        throw new System.Exception($"Fehler an Position {_position}: Erwartet {string.Join(" oder ", types)}, aber {Peek().Type} gefunden.");
    }

    private bool Check(TokenType type) => Peek().Type == type;

    // --- Haupt-Parsing-Logik ---

    public List<Statement> Parse()
    {
        var statements = new List<Statement>();
        while (Peek().Type != TokenType.EOF)
        {
            statements.Add(ParseStatement());
        }
        return statements;
    }

    private Statement ParseStatement()
    {
        // 1. "drucke" Statement
        if (Check(TokenType.Keyword) && Peek().Value == "drucke")
        {
            Match(TokenType.Keyword);
            // Wir erlauben jetzt, dass man ganze Ausdrücke drucken kann, nicht nur Variablen!
            var expr = ParseExpression(); 
            return new Drucken { ExpressionValue = expr };
        }

        // 2. Zuweisung: Variable "ist" Ausdruck
        if (Check(TokenType.Identifier))
        {
            var name = Match(TokenType.Identifier).Value;
            
            // Wir prüfen auf das Gleichheitszeichen oder das Wort "ist"
            if (Peek().Value == "=" || (Peek().Type == TokenType.Keyword && Peek().Value == "ist"))
            {
                _position++; // Überspringe "=" oder "ist"
                var value = ParseExpression();
                return new Zuweisung { VariableName = name, Wert = value };
            }
        }
        if (Check(TokenType.Keyword) && Peek().Value == "wenn")
        {
            Match(TokenType.Keyword); // "wenn"
            Match(TokenType.Separator); // "("
            var bedingung = ParseExpression();
            Match(TokenType.Separator); // ")"

            var dannBlock = ParseBlock();
            var sonstBlock = new List<Statement>();

            if (Check(TokenType.Keyword) && Peek().Value == "sonst")
            {
                Match(TokenType.Keyword);
                sonstBlock = ParseBlock();
            }

            return new WennStatement { 
                Bedingung = bedingung, 
                DannBlock = dannBlock, 
                SonstBlock = sonstBlock 
            };
        }
        throw new System.Exception($"Unbekannter Befehlsstart: {Peek().Value}");
    }
    private List<Statement> ParseBlock()
        {
            var block = new List<Statement>();
            // Wir erwarten eine geöffnete geschweifte Klammer (oder ein anderes Symbol deiner Wahl)
            // Wenn du keine Klammern willst, müsstest du ein "ende"-Keyword definieren.
    
            // Einfachheitshalber hier mit { } (Stelle sicher, dass { } im Tokenizer unter Separator sind!)
            if (Peek().Value == "{") 
            {
                Match(TokenType.Separator); // "{"
                while (Peek().Value != "}" && Peek().Type != TokenType.EOF)
                {
                    block.Add(ParseStatement());
                }
                Match(TokenType.Separator); // "}"
            }
            else 
            {
                // Ein einzelnes Statement ohne Klammern
                block.Add(ParseStatement());
            }
            return block;
        }
    // --- Ausdrücke (Rechnen) ---

    public Expression ParseExpression()
    {
        return ParseAddition();
    }

    // Ebene 1: + und -
    private Expression ParseAddition()
    {
        var left = ParseMultiplication();

        while (Peek().Value == "+" || Peek().Value == "-")
        {
            var op = Match(TokenType.Operator).Value;
            var right = ParseMultiplication();
            left = new BinaryExpr { Left = left, Operator = op, Right = right };
        }
        return left;
    }

    // Ebene 2: * und /
    private Expression ParseMultiplication()
    {
        var left = ParsePrimary();

        while (Peek().Value == "*" || Peek().Value == "/")
        {
            var op = Match(TokenType.Operator).Value;
            var right = ParsePrimary();
            left = new BinaryExpr { Left = left, Operator = op, Right = right };
        }
        return left;
    }

    // Ebene 3: Zahlen, Variablen oder Klammern
    private Expression ParsePrimary()
    {
        // Zahlen
        if (Check(TokenType.Number))
        {
            return new LiteralExpr { Value = double.Parse(Match(TokenType.Number).Value) };
        }

        // Variablen
        if (Check(TokenType.Identifier))
        {
            return new VariableExpr { Name = Match(TokenType.Identifier).Value };
        }

        // Klammern ( )
        if (Peek().Value == "(")
        {
            Match(TokenType.Separator); // (
            var expr = ParseExpression();
            Match(TokenType.Separator); // )
            return expr;
        }
        else
        {
            throw new System.Exception($"Unerwartetes Token: {Peek().Value}");
        }
    }
    }
}