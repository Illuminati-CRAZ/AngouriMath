﻿using System;
using System.Collections.Generic;
using System.Text;

namespace AngouriMath.Core.FromString
{
    using BraceType = Token.BraceType;
    using TokenType = Token.TokenType;
    internal partial class Token
    {
        public List<Entity> children { get; } = new List<Entity>();
    }
    internal static class SymbolProcessor
    {
        public static bool IsOperator(char s)
        {
            return SyntaxInfo.operatorNames.ContainsKey(s);
        }
        public static bool IsDelimiter(string s)
        {
            return s == Const.ARGUMENT_DELIMITER;
        }
    }
    internal class BraceProcessor
    {
        private readonly List<BraceType> braces = new List<BraceType>();
        public static readonly List<BraceType> parentheses = new List<BraceType>
        { 
            BraceType.PARENTHESIS_OPEN,
            BraceType.PARENTHESIS_CLOSE
        };
        internal static readonly List<BraceType> brackets = new List<BraceType>
        {
            BraceType.BRACKET_OPEN,
            BraceType.BRACKET_CLOSE
        };
        /// <summary>
        /// [ or (
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        internal static bool IsOpen(BraceType s)
        {
            return s == BraceType.BRACKET_OPEN || s == BraceType.PARENTHESIS_OPEN;
        }
        /// <summary>
        /// ((, (), )), (), [[, [].... all of the same type
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        internal static bool SameType(BraceType a, BraceType b)
        {
            return parentheses.Contains(a) && parentheses.Contains(b) ||
                   brackets.Contains(a) && brackets.Contains(b);
        }

        /// <summary>
        /// (()()[]([[]])) is a finished sequence
        /// ([]( is not
        /// </summary>
        /// <returns></returns>
        internal bool IsFinished()
        {
            return braces.Count == 0;
        }

        /// <summary>
        /// Appends a brace and checks whether everything is ok
        /// </summary>
        /// <param name="symbol"></param>
        internal void Add(BraceType symbol)
        {
            if (symbol == BraceType.NONE)
            {
                throw new ParseException("Brace error");
            }
            if (!IsOpen(symbol))
            {
                if (IsFinished())
                {
                    throw new ParseException("Brace error");
                }
                else
                {
                    if (SameType(braces[braces.Count - 1], symbol))
                    {
                        if (IsOpen(braces[braces.Count - 1]))
                        {
                            braces.RemoveAt(braces.Count - 1);
                        }
                        // otherwise is unreacheable.
                    }
                    else
                    {
                        throw new ParseException("Brace error");
                    }
                }
            }
            else
            {
                braces.Add(symbol);
            }
        }
    }
    internal static class Parser
    {
        /// <summary>
        /// Builds a tree of the expression
        /// </summary>
        /// <param name="lexer"></param>
        /// <returns></returns>
        internal static Entity Parse(Lexer lexer)
        {
            var linearExpression = new List<Entity>();
            while (!lexer.EOF())
            {
                linearExpression.Add(ParseAsVariable(lexer));
                lexer.Next();
                if (lexer.EOF())
                {
                    break;
                }
                if ((lexer.Current.Type == TokenType.PARENTHESIS_CLOSE) || (SymbolProcessor.IsDelimiter(lexer.Current.Value)))
                { 
                    break; 
                }
                if (!lexer.EOF() && !Token.IsOperator(lexer.Current.Value))
                {
                    throw new ParseException("Expected operator");
                }
                if (!lexer.EOF())
                {
                    linearExpression.Add(new OperatorEntity(SyntaxInfo.operatorNames[lexer.Current.Value[0]], SyntaxInfo.operatorPriorities[lexer.Current.Value[0]]));
                    lexer.Next();
                }

            }
            if (linearExpression.Count == 0)
            {
                throw new ParseException("Empty expression");
            }
            if (linearExpression[linearExpression.Count - 1].entType == Entity.EntType.OPERATOR &&
                linearExpression[linearExpression.Count - 1].IsLeaf) //check case `2 + 3 - `
            {
                throw new ParseException("Expected expression not to end with operator");
            }
            return HangLinearExpression(linearExpression);
        }

        /// <summary>
        /// DOCTODO
        /// </summary>
        /// <param name="expr"></param>
        /// <param name="op"></param>
        /// <param name="reversed"></param>
        private static void FindOperator(List<Entity> expr, string op, bool reversed = false)
        {
            int i = 1;
            while(i < expr.Count - 1)
            {
                var id = reversed ? expr.Count - i - 1 : i;
                if(expr[id].IsLeaf && expr[id].entType == Entity.EntType.OPERATOR && op.Contains(expr[id].Name))
                {
                    expr[id].Children.Add(expr[id - 1]);
                    expr[id].Children.Add(expr[id + 1]);
                    expr.RemoveAt(id + 1);
                    expr.RemoveAt(id - 1);
                    i--;
                }
                i++;
            }
        }

        /// <summary>
        /// Hanging "a + b * c - t ^ g"
        /// </summary>
        /// <param name="expr"></param>
        /// <returns></returns>
        private static Entity HangLinearExpression(List<Entity> expr)
        {
            FindOperator(expr, "powf", reversed: true);
            FindOperator(expr, "mulfdivf");
            FindOperator(expr, "sumfminusf");
            if (expr.Count != 1)
            {
                throw new ParseException("Tree is ambigious");
            }
            return expr[0];
        }

        /// <summary>
        /// Sequence of latin letters is "AsVariable"
        /// </summary>
        /// <param name="lexer"></param>
        /// <returns></returns>
        private static Entity ParseAsVariable(Lexer lexer)
        {
            Entity e = null;
            var current = lexer.Current;
            if (current.Type == TokenType.PARENTHESIS_OPEN)
            {
                e = ParseParenthesisExpression(lexer); // out of scope
            }
            else if (current.Type == TokenType.NUMBER)
            {
                e = new NumberEntity(Number.Parse(current.Value));
            }
            else if (current.Type == TokenType.VARIABLE || current.Type == TokenType.FUNCTION && lexer.GlanceNext().Type != TokenType.PARENTHESIS_OPEN)
            {
                e = new VariableEntity(current.Value);
            }
            else if (current.Type == TokenType.FUNCTION)
            {
                e = ParseFunctionExpression(lexer);
                lexer.Prev();
            }
            else
            {
                throw new ParseException("unexpected token");
            }
            return e;
        }

        /// <summary>
        /// f(a, b, c) is 
        /// f with three children
        /// </summary>
        /// <param name="lexer"></param>
        /// <returns></returns>
        private static Entity ParseFunctionExpression(Lexer lexer)
        {
            if (lexer.Current.Type != TokenType.FUNCTION)
            {
                throw new ParseException("function expected");
            }

            var f = new FunctionEntity(lexer.Current.Value + "f");
            lexer.Next(); // `func` -> `(`
            if (lexer.Current.Type != TokenType.PARENTHESIS_OPEN)
            {
                throw new ParseException("`(` expected after function name");
            }

            f.Children = ParseFunctionArguments(lexer);
            return f;
        }

        /// <summary>
        /// Divides a, b, c into a, b, c
        /// </summary>
        /// <param name="lexer"></param>
        /// <returns></returns>
        private static List<Entity> ParseFunctionArguments(Lexer lexer)
        {
            var args = new List<Entity>();

            if (lexer.Current.Type != TokenType.PARENTHESIS_OPEN)
            {
                throw new ParseException("`(` expected after function name");
            }
            lexer.Next(); // `(` -> args ...

            if (lexer.Current.Type == TokenType.PARENTHESIS_CLOSE)
                return args; // 0 arguments

            while (!lexer.EOF())
            {
                args.Add(Parse(lexer));
                if (lexer.Current.Type == TokenType.PARENTHESIS_CLOSE)
                {
                    lexer.Next(); // `)` -> ...
                    return args;
                }
                if (!SymbolProcessor.IsDelimiter(lexer.Current.Value))
                {
                    throw new ParseException("`,` expected between function arguments");
                }
                lexer.Next(); // `,` -> args ...
            }
            throw new ParseException("EOF reached");
        }
        private static Entity ParseParenthesisExpression(Lexer lexer)
        {
            if (lexer.Current.Type != TokenType.PARENTHESIS_OPEN)
                throw new ParseException("`(` expected");
            lexer.Next(); // `(` -> ...

            Entity e = Parse(lexer);

            if (lexer.Current.Type != TokenType.PARENTHESIS_CLOSE)
                throw new ParseException("`)` expected");

            return e;
        }
    }
}
