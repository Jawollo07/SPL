using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jugend_Forscht.Views;

// --- Daten Strukturen ---
public enum TokenType { Keyword, Identifier, Number, String, Operator, Separator, EOF }
public record Token(TokenType Type, string Value, int Line);

public abstract class Statement { public int Line { get; set; } }
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
public class EingebenStatement : Statement { public string VariableName { get; set; } = ""; }
public class WahrendStatement : Statement
{
    public Expression Bedingung { get; set; } = new LiteralExpr();
    public List<Statement> Korper { get; set; } = new();
}
public class WennStatement : Statement
{
    public Expression Bedingung { get; set; } = new LiteralExpr();
    public List<Statement> DannBlock { get; set; } = new();
    public List<Statement> SonstBlock { get; set; } = new();
}

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
            Console.WriteLine("Starte Interpreter...");
            Output.Text += "Starte Interpreter...\n";
        }
        ExecuteCode();
    }

    // --- Logik ---
    private Dictionary<string, object> _variables = new();

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
            Output.Text += $"[Fehler] {ex.Message}\n";
        }
    }

    public object Evaluate(Expression expr)
{
    return expr switch
    {
        LiteralExpr l => l.Value, // Gibt double oder string zurück
        VariableExpr v => _variables.ContainsKey(v.Name) ? _variables[v.Name] : throw new Exception($"Variable '{v.Name}' nicht definiert."),
        BinaryExpr b => EvaluateBinary(b),
        _ => 0
    };
}

private object EvaluateBinary(BinaryExpr b)
{
    object left = Evaluate(b.Left);
    object right = Evaluate(b.Right);

    // String-Konkatenation mit +
    if (b.Operator == "+" && (left is string || right is string))
    {
        return left?.ToString() + right?.ToString();
    }

    // Numerische Operationen
    double leftNum = Convert.ToDouble(left);
    double rightNum = Convert.ToDouble(right);

    return b.Operator switch
    {
        "+" => leftNum + rightNum,
        "-" => leftNum - rightNum,
        "*" => leftNum * rightNum,
        "/" => rightNum != 0 ? leftNum / rightNum : throw new Exception("Division durch Null"),
        ">" => leftNum > rightNum ? 1 : 0,
        "<" => leftNum < rightNum ? 1 : 0,
        "==" => Math.Abs(leftNum - rightNum) < 0.0001 ? 1 : 0,
        _ => throw new Exception($"Operator {b.Operator} unbekannt")
    };
}

    public void RunInterpreter(List<Statement> statements)
    {
        foreach (var smt in statements)
        {
            try
            {
                if (smt is Drucken druck)
                {
                    object result = Evaluate(druck.ExpressionValue);
                    Output.Text += $"> {result}\n";
                }
                else if (smt is Zuweisung zuweis)
                {
                    _variables[zuweis.VariableName] = Evaluate(zuweis.Wert);
                }
                else if (smt is EingebenStatement eingabe)
                {
                    // Liest vom InputField und speichert als Variable
                    string input = InputField.Text ?? "";
                    // Versuche als Zahl zu parsen, sonst als String speichern
                    if (double.TryParse(input, out double numValue))
                    {
                        _variables[eingabe.VariableName] = numValue;
                    }
                    else
                    {
                        _variables[eingabe.VariableName] = input;
                    }
                    Output.Text += $"[Eingabe] {eingabe.VariableName} = {input}\n";
                    InputField.Clear();
                }
                else if (smt is WennStatement wenn)
                {
                    // Wir prüfen: Ist die Bedingung nicht 0?
                    object bedingung = Evaluate(wenn.Bedingung);
                    if (bedingung is double d && d != 0)
                    {
                        RunInterpreter(wenn.DannBlock);
                    }
                    else if (wenn.SonstBlock.Count > 0)
                    {
                        RunInterpreter(wenn.SonstBlock);
                    }
                }
                else if (smt is WahrendStatement wahrend)
                {
                    // Wir werten die Bedingung aus. Solange sie nicht 0 ist, läuft die Schleife.
                    while (Convert.ToDouble(Evaluate(wahrend.Bedingung)) != 0)
                    {
                    // Führe den Block der Schleife aus
                    RunInterpreter(wahrend.Korper);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Output.Text += $"{ex.Message}\n"; 
            }
        }
    }   
}

    public class Tokenizer
    {
        private readonly List<(TokenType Type, string Pattern)> _definitions = new()
        {
            (TokenType.String, @"""[^""]*"""), // Strings FIRST - vor Identifiern!
            (TokenType.Keyword, @"\b(drucke|eingabe|ist|sonst|während)\b"),
            (TokenType.Number, @"\d+(?:\.\d+)?"), // Mit Float-Support
            (TokenType.Identifier, @"[a-zA-Z_][a-zA-Z0-9_]*"),
            (TokenType.Operator, @"(==|>=|<=|>|<|\+|-|\*|/|=)"), // Reihenfolge wichtig: == vor =
            (TokenType.Separator, @"[\(\);\{\}]"), // { } hinzugefügt
        };

        public List<Token> Tokenize(string input)
        {
            var tokens = new List<Token>();
            int index = 0;
            int currentLine = 1; // Wir starten in Zeile 1

            while (index < input.Length)
            {
                char c = input[index];
                if (c == '\n') { currentLine++; index++; continue; }
                if (char.IsWhiteSpace(c)) { index++; continue; }

                bool matched = false;
                foreach (var def in _definitions)
                {
                    var regex = new Regex("^" + def.Pattern);
                    var match = regex.Match(input.Substring(index));
                    if (match.Success)
                    {
                        // Hier geben wir die aktuelle Zeile mit!
                        tokens.Add(new Token(def.Type, match.Value, currentLine));
                        index += match.Length;
                        matched = true;
                        break;
                    }
                }
                if (!matched) throw new Exception($"Zeile {currentLine}: Unerwartetes Zeichen '{input[index]}'");
            }
            tokens.Add(new Token(TokenType.EOF, string.Empty, currentLine));
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
    int startLine = Peek().Line;

    if (Check(TokenType.Keyword) && Peek().Value == "drucke")
    {
        Match(TokenType.Keyword);
        // Sammle mehrere Ausdrücke bis zum Zeilenende/Block-Ende
        var expressions = new List<Expression>();
        
        while (Peek().Type != TokenType.EOF && Peek().Value != "}" && Peek().Value != ";")
        {
            expressions.Add(ParseExpression());
            // Stoppe, wenn das nächste Token ein Keyword ist (neuer Befehl)
            if (Check(TokenType.Keyword))
                break;
        }
        
        // Wenn nur ein Ausdruck, verwende ihn direkt
        if (expressions.Count == 1)
        {
            return new Drucken { ExpressionValue = expressions[0], Line = startLine };
        }
        // Wenn mehrere, verbinde sie zu einem StringConcat-Ausdruck
        else if (expressions.Count > 1)
        {
            var combined = expressions[0];
            foreach (var expr in expressions.Skip(1))
            {
                combined = new BinaryExpr { Left = combined, Operator = "+", Right = expr };
            }
            return new Drucken { ExpressionValue = combined, Line = startLine };
        }
        else
        {
            throw new Exception($"Zeile {startLine}: 'drucke' benötigt mindestens einen Ausdruck");
        }
    }

    if (Check(TokenType.Identifier))
    {
        var name = Match(TokenType.Identifier).Value;
        if (Peek().Value == "=" || (Peek().Type == TokenType.Keyword && Peek().Value == "ist"))
        {
            _position++; 
            var value = ParseExpression();
            return new Zuweisung { VariableName = name, Wert = value, Line = startLine };
        }
        else
        {
            throw new Exception($"Zeile {startLine}: Identifier '{name}' benötigt '=' oder 'ist' für Zuweisung");
        }
    }

    if (Check(TokenType.Keyword) && Peek().Value == "eingabe")
    {
        Match(TokenType.Keyword);
        var varName = Match(TokenType.Identifier).Value;
        return new EingebenStatement { VariableName = varName, Line = startLine };
    }
    if (Check(TokenType.Keyword) && Peek().Value == "während")
    {
        Match(TokenType.Keyword); // "während"
        Match(TokenType.Separator); // "("
        var bedingung = ParseExpression();
        Match(TokenType.Separator); // ")"

        var korper = ParseBlock(); // Nutzt die vorhandene ParseBlock-Logik

        return new WahrendStatement { 
            Bedingung = bedingung, 
            Korper = korper, 
            Line = startLine 
        };
    }
    if (Check(TokenType.Keyword) && Peek().Value == "wenn")
    {
        Match(TokenType.Keyword);
        Match(TokenType.Separator); // (
        var bedingung = ParseExpression();
        Match(TokenType.Separator); // )

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
            SonstBlock = sonstBlock,
            Line = startLine 
        };
    }
    throw new Exception($"Zeile {startLine}: Unbekannter Befehl '{Peek().Value}'");
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
        // Strings
        if (Check(TokenType.String))
        {
            String val = Match(TokenType.String).Value;
            return new LiteralExpr {Value = val.Trim('"')}; // Entfernt die Anführungszeichen
        }
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