using System;
using System.Collections.Generic;
using System.Text;

namespace Tailspin.DealerOrders.Configurator.Rules
{
    public enum TokenKind
    {
        Option,
        And,
        Or,
        Not,
        LParen,
        RParen,
        Comma,
        Arrow,
        Requires,
        Excludes,
        Includes,
        Any,
        Market,
        Invalid,
        Text,
        End
    }

    public struct Token
    {
        public Token(TokenKind kind, string value, int position)
        {
            Kind = kind;
            Value = value;
            Position = position;
        }

        public TokenKind Kind { get; }
        public string Value { get; }
        public int Position { get; }

        public override string ToString() => Kind + (Value == null ? "" : "(" + Value + ")");
    }

    /// <summary>
    /// Tokenizer for configuration rule expressions, for example
    /// <c>CRANE-11M and (CAB-LEVEL or CAB-ROTATE) and not market(US, CA) =&gt; requires FIRE-SUPP</c>.
    /// </summary>
    public static class RuleLexer
    {
        private static readonly Dictionary<string, TokenKind> Keywords = new Dictionary<string, TokenKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["and"] = TokenKind.And,
            ["or"] = TokenKind.Or,
            ["not"] = TokenKind.Not,
            ["requires"] = TokenKind.Requires,
            ["excludes"] = TokenKind.Excludes,
            ["includes"] = TokenKind.Includes,
            ["any"] = TokenKind.Any,
            ["market"] = TokenKind.Market,
            ["invalid"] = TokenKind.Invalid
        };

        public static List<Token> Tokenize(string text)
        {
            var tokens = new List<Token>();
            var i = 0;
            while (i < text.Length)
            {
                var c = text[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                switch (c)
                {
                    case '(':
                        tokens.Add(new Token(TokenKind.LParen, null, i++));
                        continue;
                    case ')':
                        tokens.Add(new Token(TokenKind.RParen, null, i++));
                        continue;
                    case ',':
                        tokens.Add(new Token(TokenKind.Comma, null, i++));
                        continue;
                    case '&':
                        if (i + 1 < text.Length && text[i + 1] == '&') { tokens.Add(new Token(TokenKind.And, null, i)); i += 2; continue; }
                        break;
                    case '|':
                        if (i + 1 < text.Length && text[i + 1] == '|') { tokens.Add(new Token(TokenKind.Or, null, i)); i += 2; continue; }
                        break;
                    case '!':
                        tokens.Add(new Token(TokenKind.Not, null, i++));
                        continue;
                    case '=':
                        if (i + 1 < text.Length && text[i + 1] == '>') { tokens.Add(new Token(TokenKind.Arrow, null, i)); i += 2; continue; }
                        break;
                    case '"':
                        {
                            var start = i++;
                            var sb = new StringBuilder();
                            while (i < text.Length && text[i] != '"') sb.Append(text[i++]);
                            if (i >= text.Length) throw new RuleSyntaxException("Unterminated text", start);
                            i++;
                            tokens.Add(new Token(TokenKind.Text, sb.ToString(), start));
                            continue;
                        }
                }

                if (char.IsLetterOrDigit(c))
                {
                    var start = i;
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '-' || text[i] == '_' || text[i] == '.')) i++;
                    var word = text.Substring(start, i - start);
                    tokens.Add(Keywords.TryGetValue(word, out var kind)
                        ? new Token(kind, word, start)
                        : new Token(TokenKind.Option, word.ToUpperInvariant(), start));
                    continue;
                }

                throw new RuleSyntaxException($"Unexpected character '{c}'", i);
            }

            tokens.Add(new Token(TokenKind.End, null, text.Length));
            return tokens;
        }
    }

    [Serializable]
    public class RuleSyntaxException : Exception
    {
        public RuleSyntaxException(string message, int position) : base($"{message} at position {position}.")
        {
            Position = position;
        }

        protected RuleSyntaxException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context) { }

        public int Position { get; }
    }
}
