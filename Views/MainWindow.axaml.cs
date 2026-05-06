using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Jugend_Forscht.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    public void Run(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ExecuteCode();
    }
    public enum TokenType
    {
        Keyword,
        Identifier,
        Number,
        String,
        Operator,
        Separator,
        EOF
    }
    public record Token(TokenType Type, string Value);
   public class Tokenizer
{
    private readonly List<(TokenType Type, string Pattern)> _definitions = new()
    {
        (TokenType.Number, @"\d+"),
        (TokenType.Keyword, @"\b(drucke|ist|sonst|während)\b"),
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
            // Überspringe Whitespace (Leerzeichen, Tabs, Umbrüche)
            if (char.IsWhiteSpace(input[index]))
            {
                index++;
                continue;
            }

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

            if (!matched)
            {
                throw new System.Exception($"Unerwartetes Zeichen: {input[index]} an Stelle {index}");
            }
        }

        tokens.Add(new Token(TokenType.EOF, string.Empty));
        return tokens;
    }
}
    public void ExecuteCode()
    {
        //Hier kommt der Code hin, der ausgeführt werden soll, wenn der "Ausführen" Button geklickt wird.
        string code = Editor.Text;
        try
        {
            var tokenizer = new Tokenizer();
            var tokens = tokenizer.Tokenize(code);
        }
        catch (System.Exception ex)
        {
            // Fehlerbehandlung, z.B. Fehlermeldung anzeigen
            Output.Text += $"Fehler: {ex.Message}\n";
        }
    } 
}