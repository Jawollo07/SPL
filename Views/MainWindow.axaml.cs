using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.Rendering;
using System.Xml;

namespace Jugend_Forscht.Views;

// --- Daten Strukturen ---
public enum TokenType { Keyword, Identifier, Number, String, Operator, Separator, Comment, EOF }
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
public class Leeren : Statement { }
public class Drucken : Statement { public Expression ExpressionValue { get; set; } = new LiteralExpr(); }
public class Zuweisung : Statement { public string VariableName { get; set; } = ""; public Expression Wert { get; set; } = new LiteralExpr(); }
public class EingebenStatement : Statement { public string VariableName { get; set; } = ""; }
public class ZeichnenStatement : Statement 
{ 
    public string FormType { get; set; } = ""; // kreis, rechteck, linie, stern
    public List<Expression> Parameter { get; set; } = new();
}
public class WahrendStatement : Statement
{
    public Expression Bedingung { get; set; } = new LiteralExpr();
    public List<Statement> Korper { get; set; } = new();
}
public class WennStatement : Statement { public Expression Bedingung { get; set; } = new LiteralExpr(); public List<Statement> DannBlock { get; set; } = new(); public List<Statement> SonstBlock { get; set; } = new(); }

public partial class MainWindow : Window
{
    private ErrorLineRenderer _errorRenderer;
    public MainWindow()
    {
        InitializeComponent();
        SetupSyntaxHighlighting(); // Hier aktivieren wir die Farben
        // Renderer registrieren
        _errorRenderer = new ErrorLineRenderer(Editor);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_errorRenderer);
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
    // Style für Syntax-Highlighting laden
    private void SetupSyntaxHighlighting()
    {
        // Wir definieren die Farben für deine Sprache "SPL"
        string xshdContent = @"
        <SyntaxDefinition name=""SPL"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
            <Color name=""Keywords"" foreground=""#569DAA"" fontWeight=""bold"" />
            <Color name=""Strings"" foreground=""#D69D85"" />
            <Color name=""Numbers"" foreground=""#B5CEA8"" />
            <Color name=""Comments"" foreground=""#57A64A"" />

            <RuleSet>
                <Keywords color=""Keywords"">
                    <Word>drucke</Word>
                    <Word>ist</Word>
                    <Word>wenn</Word>
                    <Word>sonst</Word>
                    <Word>während</Word>
                    <Word>eingeben</Word>
                    <Word>exit</Word>
                    <Word>warte</Word>
                </Keywords>

                <Span color=""Strings"">
                    <Begin>""</Begin>
                    <End>""</End>
                </Span>

                <Rule color=""Numbers"">
                    \b\d+(\.\d+)?\b
                </Rule>
            </RuleSet>
        </SyntaxDefinition>";

        using (var reader = new XmlTextReader(new System.IO.StringReader(xshdContent)))
        {
            Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
    }
    // --- Logik ---
    private Dictionary<string, object> _variables = new();

    public void ExecuteCode()
    {
        try 
        {      
            // 1. Alte Fehler-Markierung entfernen
            _errorRenderer.ErrorLine = null;
            Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
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
            // 2. Zeile aus der Fehlermeldung extrahieren (wenn vorhanden)
            // Wir suchen nach dem Muster "Zeile X" in deiner Exception-Message
            var match = Regex.Match(ex.Message, @"Zeile (\d+)");
            if (match.Success)
            {
                _errorRenderer.ErrorLine = int.Parse(match.Groups[1].Value);
            }
            Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
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
                else if (smt is Leeren)
                {
                    Output.Text = "";
                    DrawingCanvas.Children.Clear();
                }
                else if (smt is ZeichnenStatement zeichnen)
                {
                    try
                    {
                        DrawShape(zeichnen);
                    }
                    catch (System.Exception ex)
                    {
                        Output.Text += $"[Zeichnen-Fehler] {ex.Message}\n";
                    }
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
                else if (smt is Zuweisung z && z.VariableName == "__wait_duration")
                {
                    // Spezielle Behandlung für Warte-Zuweisungen
                    object durationObj = Evaluate(z.Wert);
                    if (durationObj is double duration)
                    {
                        System.Threading.Thread.Sleep((int)(duration * 1000)); // Wartezeit in Millisekunden
                    }
                    else
                    {
                        throw new Exception($"Zeile {z.Line}: Wartezeit muss eine Zahl sein");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Output.Text += $"{ex.Message}\n"; 
            }
        }
    }   

    private void DrawShape(ZeichnenStatement zeichnen)
    {
        if (zeichnen.Parameter.Count < 3)
            throw new Exception("Zu wenige Parameter für Zeichenbefehl");

        double x = Convert.ToDouble(Evaluate(zeichnen.Parameter[0]));
        double y = Convert.ToDouble(Evaluate(zeichnen.Parameter[1]));
        double size = Convert.ToDouble(Evaluate(zeichnen.Parameter[2]));

        var brush = new SolidColorBrush(Colors.LimeGreen);
        var pen = new Pen(brush, 2);

        switch (zeichnen.FormType.ToLower())
        {
            case "kreis":
                {
                    var circle = new Ellipse
                    {
                        Width = size,
                        Height = size,
                        Fill = null,
                        Stroke = brush,
                        StrokeThickness = 2
                    };
                    Canvas.SetLeft(circle, x - size / 2);
                    Canvas.SetTop(circle, y - size / 2);
                    DrawingCanvas.Children.Add(circle);
                    Output.Text += $"[Zeichnung] Kreis bei ({x}, {y}) mit Größe {size}\n";
                    break;
                }
            case "rechteck":
                {
                    if (zeichnen.Parameter.Count < 4)
                        throw new Exception("Rechteck benötigt: x1 y1 x2 y2");
                    double x2 = Convert.ToDouble(Evaluate(zeichnen.Parameter[3]));
                    double y2 = Convert.ToDouble(Evaluate(zeichnen.Parameter[2]));

                    var rect = new Rectangle
                    {
                        Width = Math.Abs(x2 - x),
                        Height = Math.Abs(y2 - y),
                        Fill = null,
                        Stroke = brush,
                        StrokeThickness = 2
                    };
                    Canvas.SetLeft(rect, Math.Min(x, x2));
                    Canvas.SetTop(rect, Math.Min(y, y2));
                    DrawingCanvas.Children.Add(rect);
                    Output.Text += $"[Zeichnung] Rechteck von ({x}, {y}) zu ({x2}, {y2})\n";
                    break;
                }
            case "linie":
                {
                    if (zeichnen.Parameter.Count < 4)
                        throw new Exception("Linie benötigt: x1 y1 x2 y2");
                    double x2 = Convert.ToDouble(Evaluate(zeichnen.Parameter[3]));
                    double y2 = Convert.ToDouble(Evaluate(zeichnen.Parameter[2]));

                    var line = new Line
                    {
                        StartPoint = new Point(x, y),
                        EndPoint = new Point(x2, y2),
                        Stroke = brush,
                        StrokeThickness = 2
                    };
                    DrawingCanvas.Children.Add(line);
                    Output.Text += $"[Zeichnung] Linie von ({x}, {y}) zu ({x2}, {y2})\n";
                    break;
                }
            case "stern":
                {
                    DrawStar(x, y, size, brush);
                    Output.Text += $"[Zeichnung] Stern bei ({x}, {y}) mit Größe {size}\n";
                    break;
                }
            default:
                throw new Exception($"Unbekannte Form: {zeichnen.FormType}");
        }
    }

    private void DrawStar(double centerX, double centerY, double size, Brush brush)
    {
        const int points = 5;
        var geometry = new StreamGeometry();

        using (var context = geometry.Open())
        {
            for (int i = 0; i < points * 2; i++)
            {
                double angle = (i * Math.PI) / points - Math.PI / 2;
                double radius = (i % 2 == 0) ? size : size / 2;
                double px = centerX + radius * Math.Cos(angle);
                double py = centerY + radius * Math.Sin(angle);

                if (i == 0)
                    context.BeginFigure(new Point(px, py), true);
                else
                    context.LineTo(new Point(px, py));
            }
        }

        var path = new Path
        {
            Data = geometry,
            Fill = null,
            Stroke = brush,
            StrokeThickness = 2
        };
        DrawingCanvas.Children.Add(path);
    }
}

    public class Tokenizer
    {
        private readonly List<(TokenType Type, string Pattern)> _definitions = new()
        {
            (TokenType.Comment, @"#.*"), // Ignoriert Kommentare
            (TokenType.String, @"""[^""]*"""), // Strings FIRST - vor Identifiern!
            (TokenType.Keyword, @"\b(drucke|leeren|eingabe|ist|sonst|während|exit|warte|zeichne)\b"), // Keywords vor Identifiern!
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
                        if (def.Type != TokenType.Comment)
                        {
                            tokens.Add(new Token(def.Type, match.Value, currentLine));
                        }
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
    if (Check(TokenType.Comment))
    {
        Match(TokenType.Comment); // Kommentare überspringen
        return ParseStatement(); // Nächstes Statement parsen
    }
    if (Check(TokenType.Keyword) && Peek().Value == "warte")
    {
        Match(TokenType.Keyword); // "warte"
        var duration = ParseExpression();
        return new Zuweisung { VariableName = "__wait_duration", Wert = duration, Line = startLine }; // Speichert die Wartezeit in einer speziellen Variable
    }
    if (Check(TokenType.Keyword) && Peek().Value == "exit")
    {
        Match(TokenType.Keyword);
        System.Environment.Exit(0); // Beendet die Anwendung sofort
        return null; // Dieser Code wird nie erreicht, aber der Parser erwartet einen Rückgabewert
    }
    if (Check(TokenType.Keyword) && Peek().Value == "leeren")
    {
        Match(TokenType.Keyword);
        return new Leeren { Line = startLine };
    }
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

    if (Check(TokenType.Keyword) && Peek().Value == "zeichne")
    {
        Match(TokenType.Keyword); // "zeichne"
        var formType = Match(TokenType.String).Value.Trim('"');
        var parameters = new List<Expression>();
        
        // Sammle alle Parameter bis zum Zeilenende/Keyword
        while (Peek().Type != TokenType.EOF && Peek().Value != "}" && !Check(TokenType.Keyword))
        {
            parameters.Add(ParseExpression());
            if (Check(TokenType.Keyword))
                break;
        }

        return new ZeichnenStatement { FormType = formType, Parameter = parameters, Line = startLine };
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
public class ErrorLineRenderer : IBackgroundRenderer
{
    private TextEditor _editor;
    public int? ErrorLine { get; set; }

    public ErrorLineRenderer(TextEditor editor) => _editor = editor;

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (ErrorLine == null || ErrorLine < 1 || ErrorLine > _editor.Document.LineCount) return;

        var line = _editor.Document.GetLineByNumber(ErrorLine.Value);
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, line))
        {
            // Ein transparentes Rot, damit der Text lesbar bleibt
            drawingContext.DrawRectangle(new SolidColorBrush(Color.Parse("#44FF0000")), null, rect);
        }
    }
}