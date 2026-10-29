using System.Collections.Generic;

namespace Tailspin.DealerOrders.Configurator.Rules
{
    /// <summary>
    /// Recursive descent parser for rule expressions.
    /// <code>
    /// rule        := condition '=&gt;' consequence
    /// condition   := and ('or' and)*
    /// and         := unary ('and' unary)*
    /// unary       := 'not' unary | primary
    /// primary     := OPTION | 'market' '(' OPTION (',' OPTION)* ')' | '(' condition ')'
    /// consequence := ('requires' | 'excludes' | 'includes') OPTION
    ///              | 'requires' 'any' '(' OPTION (',' OPTION)* ')'
    ///              | 'invalid'
    ///              followed by an optional "message"
    /// </code>
    /// </summary>
    public sealed class RuleParser
    {
        private readonly List<Token> _tokens;
        private int _pos;

        private RuleParser(string text)
        {
            _tokens = RuleLexer.Tokenize(text);
        }

        public static CompiledRule Parse(string id, string text)
        {
            var parser = new RuleParser(text);
            var condition = parser.ParseCondition();
            parser.Expect(TokenKind.Arrow, "'=>'");
            var consequence = parser.ParseConsequence();
            parser.Expect(TokenKind.End, "end of rule");
            return new CompiledRule(id, condition, consequence, text);
        }

        public static Condition ParseConditionOnly(string text)
        {
            var parser = new RuleParser(text);
            var condition = parser.ParseCondition();
            parser.Expect(TokenKind.End, "end of condition");
            return condition;
        }

        private Token Peek => _tokens[_pos];

        private Token Next() => _tokens[_pos++];

        private Token Expect(TokenKind kind, string what)
        {
            var token = Next();
            if (token.Kind != kind) throw new RuleSyntaxException($"Expected {what} but found {token}", token.Position);
            return token;
        }

        private Condition ParseCondition()
        {
            var left = ParseAnd();
            while (Peek.Kind == TokenKind.Or)
            {
                Next();
                left = new BinaryCondition(false, left, ParseAnd());
            }
            return left;
        }

        private Condition ParseAnd()
        {
            var left = ParseUnary();
            while (Peek.Kind == TokenKind.And)
            {
                Next();
                left = new BinaryCondition(true, left, ParseUnary());
            }
            return left;
        }

        private Condition ParseUnary()
        {
            if (Peek.Kind == TokenKind.Not)
            {
                Next();
                return new NotCondition(ParseUnary());
            }
            return ParsePrimary();
        }

        private Condition ParsePrimary()
        {
            var token = Next();
            switch (token.Kind)
            {
                case TokenKind.Option:
                    return new OptionCondition(token.Value);
                case TokenKind.Market:
                    return new MarketCondition(ParseList());
                case TokenKind.LParen:
                    var inner = ParseCondition();
                    Expect(TokenKind.RParen, "')'");
                    return inner;
                default:
                    throw new RuleSyntaxException($"Unexpected {token}", token.Position);
            }
        }

        private List<string> ParseList()
        {
            Expect(TokenKind.LParen, "'('");
            var items = new List<string> { Expect(TokenKind.Option, "a code").Value };
            while (Peek.Kind == TokenKind.Comma)
            {
                Next();
                items.Add(Expect(TokenKind.Option, "a code").Value);
            }
            Expect(TokenKind.RParen, "')'");
            return items;
        }

        private Consequence ParseConsequence()
        {
            var token = Next();
            ConsequenceKind kind;
            List<string> options;
            switch (token.Kind)
            {
                case TokenKind.Requires:
                    if (Peek.Kind == TokenKind.Any)
                    {
                        Next();
                        kind = ConsequenceKind.RequiresAny;
                        options = ParseList();
                    }
                    else
                    {
                        kind = ConsequenceKind.Requires;
                        options = new List<string> { Expect(TokenKind.Option, "an option").Value };
                    }
                    break;
                case TokenKind.Excludes:
                    kind = ConsequenceKind.Excludes;
                    options = new List<string> { Expect(TokenKind.Option, "an option").Value };
                    break;
                case TokenKind.Includes:
                    kind = ConsequenceKind.Includes;
                    options = new List<string> { Expect(TokenKind.Option, "an option").Value };
                    break;
                case TokenKind.Invalid:
                    kind = ConsequenceKind.Invalid;
                    options = new List<string>();
                    break;
                default:
                    throw new RuleSyntaxException($"Expected requires, excludes, includes or invalid but found {token}", token.Position);
            }

            string message = null;
            if (Peek.Kind == TokenKind.Text) message = Next().Value;
            return new Consequence(kind, options, message);
        }
    }
}
