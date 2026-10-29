using System.Globalization;
using System.Text;

namespace StudyData.Core.EditChecks;

public enum TokenKind
{
    Number,
    String,
    Identifier,
    Operator,
    LeftParen,
    RightParen,
    Comma,
    End
}

public readonly record struct Token(TokenKind Kind, string Text, int Position);

public class ExpressionException : Exception
{
    public ExpressionException(string message, int position) : base($"{message} (at {position + 1})")
    {
        Position = position;
    }

    public int Position { get; }
}

public static class Lexer
{
    private static readonly string[] Operators = { "<=", ">=", "<>", "!=", "==", "=", "<", ">", "+", "-", "*", "/" };

    public static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (char.IsDigit(c) || (c == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
            {
                var start = i;
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Number, text[start..i], start));
                continue;
            }

            if (c is '\'' or '"')
            {
                var start = i++;
                var sb = new StringBuilder();
                while (i < text.Length && text[i] != c)
                {
                    sb.Append(text[i++]);
                }

                if (i >= text.Length)
                {
                    throw new ExpressionException("Unterminated string", start);
                }

                i++;
                tokens.Add(new Token(TokenKind.String, sb.ToString(), start));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] is '_' or '.'))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Identifier, text[start..i], start));
                continue;
            }

            switch (c)
            {
                case '(':
                    tokens.Add(new Token(TokenKind.LeftParen, "(", i++));
                    continue;
                case ')':
                    tokens.Add(new Token(TokenKind.RightParen, ")", i++));
                    continue;
                case ',':
                    tokens.Add(new Token(TokenKind.Comma, ",", i++));
                    continue;
            }

            var op = Operators.FirstOrDefault(o => string.CompareOrdinal(text, i, o, 0, o.Length) == 0);
            if (op is null)
            {
                throw new ExpressionException($"Unexpected character '{c}'", i);
            }

            tokens.Add(new Token(TokenKind.Operator, op, i));
            i += op.Length;
        }

        tokens.Add(new Token(TokenKind.End, "", text.Length));
        return tokens;
    }
}

public abstract record Node(int Position);

public sealed record NumberNode(decimal Value, int Position) : Node(Position);

public sealed record StringNode(string Value, int Position) : Node(Position);

public sealed record FieldNode(string Name, int Position) : Node(Position);

public sealed record UnaryNode(string Operator, Node Operand, int Position) : Node(Position);

public sealed record BinaryNode(string Operator, Node Left, Node Right, int Position) : Node(Position);

public sealed record CallNode(string Function, IReadOnlyList<Node> Arguments, int Position) : Node(Position);

/// <summary>
/// Recursive descent parser for edit check expressions.
/// <code>
/// or      := and ('OR' and)*
/// and     := not ('AND' not)*
/// not     := 'NOT' not | compare
/// compare := add (('=' | '==' | '&lt;&gt;' | '!=' | '&lt;' | '&lt;=' | '&gt;' | '&gt;=') add)?
/// add     := mul (('+' | '-') mul)*
/// mul     := unary (('*' | '/') unary)*
/// unary   := '-' unary | primary
/// primary := number | string | TRUE | FALSE | NULL | ident | ident '(' args ')' | '(' or ')'
/// </code>
/// </summary>
public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;

    private Parser(List<Token> tokens)
    {
        _tokens = tokens;
    }

    public static Node Parse(string expression)
    {
        var parser = new Parser(Lexer.Tokenize(expression));
        var node = parser.ParseOr();
        if (parser.Peek.Kind != TokenKind.End)
        {
            throw new ExpressionException($"Unexpected '{parser.Peek.Text}'", parser.Peek.Position);
        }

        return node;
    }

    private Token Peek => _tokens[_pos];

    private Token Next() => _tokens[_pos++];

    private bool IsKeyword(string keyword) =>
        Peek.Kind == TokenKind.Identifier && string.Equals(Peek.Text, keyword, StringComparison.OrdinalIgnoreCase);

    private Node ParseOr()
    {
        var left = ParseAnd();
        while (IsKeyword("OR"))
        {
            var t = Next();
            left = new BinaryNode("OR", left, ParseAnd(), t.Position);
        }

        return left;
    }

    private Node ParseAnd()
    {
        var left = ParseNot();
        while (IsKeyword("AND"))
        {
            var t = Next();
            left = new BinaryNode("AND", left, ParseNot(), t.Position);
        }

        return left;
    }

    private Node ParseNot()
    {
        if (IsKeyword("NOT"))
        {
            var t = Next();
            return new UnaryNode("NOT", ParseNot(), t.Position);
        }

        return ParseCompare();
    }

    private Node ParseCompare()
    {
        var left = ParseAdd();
        if (Peek.Kind == TokenKind.Operator && Peek.Text is "=" or "==" or "<>" or "!=" or "<" or "<=" or ">" or ">=")
        {
            var t = Next();
            var op = t.Text switch { "==" => "=", "!=" => "<>", _ => t.Text };
            return new BinaryNode(op, left, ParseAdd(), t.Position);
        }

        return left;
    }

    private Node ParseAdd()
    {
        var left = ParseMul();
        while (Peek.Kind == TokenKind.Operator && Peek.Text is "+" or "-")
        {
            var t = Next();
            left = new BinaryNode(t.Text, left, ParseMul(), t.Position);
        }

        return left;
    }

    private Node ParseMul()
    {
        var left = ParseUnary();
        while (Peek.Kind == TokenKind.Operator && Peek.Text is "*" or "/")
        {
            var t = Next();
            left = new BinaryNode(t.Text, left, ParseUnary(), t.Position);
        }

        return left;
    }

    private Node ParseUnary()
    {
        if (Peek.Kind == TokenKind.Operator && Peek.Text == "-")
        {
            var t = Next();
            return new UnaryNode("-", ParseUnary(), t.Position);
        }

        return ParsePrimary();
    }

    private Node ParsePrimary()
    {
        var t = Next();
        switch (t.Kind)
        {
            case TokenKind.Number:
                if (!decimal.TryParse(t.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                {
                    throw new ExpressionException($"Invalid number '{t.Text}'", t.Position);
                }

                return new NumberNode(d, t.Position);
            case TokenKind.String:
                return new StringNode(t.Text, t.Position);
            case TokenKind.LeftParen:
                var inner = ParseOr();
                Expect(TokenKind.RightParen, ")");
                return inner;
            case TokenKind.Identifier:
                if (Peek.Kind == TokenKind.LeftParen)
                {
                    Next();
                    var args = new List<Node>();
                    if (Peek.Kind != TokenKind.RightParen)
                    {
                        do
                        {
                            args.Add(ParseOr());
                        }
                        while (Peek.Kind == TokenKind.Comma && Next().Kind == TokenKind.Comma);
                    }

                    Expect(TokenKind.RightParen, ")");
                    return new CallNode(t.Text.ToUpperInvariant(), args, t.Position);
                }

                return new FieldNode(t.Text, t.Position);
            default:
                throw new ExpressionException(t.Kind == TokenKind.End ? "Unexpected end of expression" : $"Unexpected '{t.Text}'", t.Position);
        }
    }

    private void Expect(TokenKind kind, string text)
    {
        var t = Next();
        if (t.Kind != kind)
        {
            throw new ExpressionException($"Expected '{text}'", t.Position);
        }
    }
}
