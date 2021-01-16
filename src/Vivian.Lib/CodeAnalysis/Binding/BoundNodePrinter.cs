﻿using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq.Expressions;
using Vivian.CodeAnalysis.Symbols;
using Vivian.CodeAnalysis.Syntax;
using Vivian.IO;

namespace Vivian.CodeAnalysis.Binding
{
    internal static class BoundNodePrinter
    {
        public static void WriteTo(this BoundNode node, TextWriter writer)
        {
            if (writer is IndentedTextWriter iw)
                WriteTo(node, iw);
            else
                WriteTo(node, new IndentedTextWriter(writer));
        }
        
        
        private static void WriteNestedStatement(this IndentedTextWriter writer, BoundStatement node)
        {
            var needsIndentation = !(node is BoundBlockStatement);
            if (needsIndentation)
                writer.Indent++;
            
            node.WriteTo(writer);

            if (needsIndentation)
                writer.Indent--;
        }
        
        private static void WriteNestedExpression(this IndentedTextWriter writer, int parentPrecedence, BoundExpression expression)
        {
            if (expression is BoundUnaryExpression unary)
                writer.WriteNestedExpression(parentPrecedence, SyntaxFacts.GetUnaryOperatorPrecedence(unary.Op.SyntaxKind), unary);
            
            else if (expression is BoundBinaryExpression binary)
                writer.WriteNestedExpression(parentPrecedence, SyntaxFacts.GetBinaryOperatorPrecedence(binary.Op.SyntaxKind), binary);
            
            else
                expression.WriteTo(writer);
        }
        
        private static void WriteNestedExpression(this IndentedTextWriter writer, int parentPrecedence, int currentPrecedence, BoundExpression expression)
        {
            var needsParenthesis = parentPrecedence >= currentPrecedence;
            
            if (needsParenthesis)
                writer.WritePunctuation(SyntaxKind.OpenParenthesisToken);

            expression.WriteTo(writer);
            
            if (needsParenthesis)
                writer.WritePunctuation(SyntaxKind.CloseParenthesisToken);
        }

        public static void WriteTo(this BoundNode node, IndentedTextWriter writer)
        {
            switch (node.Kind)
            {
                case BoundNodeKind.LiteralExpression:
                    WriteLiteralExpression((BoundLiteralExpression) node, writer);
                    break;
                case BoundNodeKind.VariableExpression:
                    WriteVariableExpression((BoundVariableExpression) node, writer);
                    break;
                case BoundNodeKind.AssignmentExpression:
                    WriteAssignmentExpression((BoundAssignmentExpression) node, writer);
                    break;
                case BoundNodeKind.UnaryExpression:
                    WriteUnaryExpression((BoundUnaryExpression) node, writer);
                    break;
                case BoundNodeKind.BinaryExpression:
                    WriteBinaryExpression((BoundBinaryExpression) node, writer);
                    break;
                case BoundNodeKind.ErrorExpression:
                    WriteErrorExpression((BoundErrorExpression) node, writer);
                    break;
                case BoundNodeKind.CallExpression:
                    WriteCallExpression((BoundCallExpression) node, writer);
                    break;
                case BoundNodeKind.ConversionExpression:
                    WriteConversionExpression((BoundConversionExpression) node, writer);
                    break;
                case BoundNodeKind.BlockStatement:
                    WriteBlockStatement((BoundBlockStatement) node, writer);
                    break;
                case BoundNodeKind.ExpressionStatement:
                    WriteExpressionStatement((BoundExpressionStatement) node, writer);
                    break;
                case BoundNodeKind.IfStatement:
                    WriteIfStatement((BoundIfStatement) node, writer);
                    break;
                case BoundNodeKind.DoWhileStatement:
                    WriteDoWhileStatement((BoundDoWhileStatement) node, writer);
                    break;
                case BoundNodeKind.WhileStatement:
                    WriteWhileStatement((BoundWhileStatement) node, writer);
                    break;
                case BoundNodeKind.ForStatement:
                    WriteForStatement((BoundForStatement) node, writer);
                    break;
                case BoundNodeKind.VariableDeclaration:
                    WriteVariableDeclaration((BoundVariableDeclaration) node, writer);
                    break;
                case BoundNodeKind.GotoStatement:
                    WriteGotoStatement((BoundGotoStatement) node, writer);
                    break;
                case BoundNodeKind.LabelStatement:
                    WriteLabelStatement((BoundLabelStatement) node, writer);
                    break;
                case BoundNodeKind.ConditionalGotoStatement:
                    WriteConditionalGotoStatement((BoundConditionalGotoStatement) node, writer);
                    break;
                case BoundNodeKind.ReturnStatement:
                    WriteReturnStatement((BoundReturnStatement) node, writer);
                    break;
                default:
                    throw new Exception($"Unexpected node: {node.Kind}");
            }
        }

        private static void WriteReturnStatement(BoundReturnStatement node, IndentedTextWriter writer)
        {
            writer.WriteKeyword(SyntaxKind.ReturnKeyword);
            writer.WriteSpace();
            if (node.Expression != null)
            {
                node.Expression.WriteTo(writer);
            }
            writer.WriteLine();
        }

        private static void WriteLiteralExpression(BoundLiteralExpression node, IndentedTextWriter writer)
        {
            var value = node.Value.ToString();
            if (node.Type == TypeSymbol.Bool)
            {
                writer.WriteKeyword(value);
            }
            
            else if (node.Type == TypeSymbol.Int)
            {
                writer.WriteNumber(value);
            }
            
            else if (node.Type == TypeSymbol.String)
            {
                value = "\"" + value.Replace("\"", "\"\"") + "\"";
                writer.WriteString(value);
            }
            else
                throw new Exception($"Unexpected type: {node.Type}");
        }

        private static void WriteVariableExpression(BoundVariableExpression node, IndentedTextWriter writer)
        {
            writer.WriteIdentifier(node.Variable.Name);
        }

        private static void WriteAssignmentExpression(BoundAssignmentExpression node, IndentedTextWriter writer)
        {
            writer.WriteIdentifier(node.Variable.Name);
            writer.WriteSpace();
            writer.WritePunctuation(SyntaxKind.EqualsToken);
            writer.WriteSpace();
            node.Expression.WriteTo(writer);
        }

        private static void WriteUnaryExpression(BoundUnaryExpression node, IndentedTextWriter writer)
        {
            var op = SyntaxFacts.GetText(node.Op.SyntaxKind);
            var precedence = SyntaxFacts.GetUnaryOperatorPrecedence(node.Op.SyntaxKind);
            
            writer.WritePunctuation(op);
            
            writer.WriteNestedExpression(precedence, node.Operand);
        }

        private static void WriteBinaryExpression(BoundBinaryExpression node, IndentedTextWriter writer)
        {
            var op = SyntaxFacts.GetText(node.Op.SyntaxKind);
            var precedence = SyntaxFacts.GetBinaryOperatorPrecedence(node.Op.SyntaxKind);
            
            writer.WriteNestedExpression(precedence, node.Left);
            writer.WriteSpace();
            writer.WritePunctuation(op);
            writer.WriteSpace();
            writer.WriteNestedExpression(precedence, node.Right);
        }

        private static void WriteErrorExpression(BoundErrorExpression node, IndentedTextWriter writer)
        {
            writer.WriteKeyword("?");
        }

        private static void WriteCallExpression(BoundCallExpression node, IndentedTextWriter writer)
        {
            writer.WriteIdentifier(node.Function.Name);
            writer.WritePunctuation(SyntaxKind.OpenParenthesisToken);

            var isFirst = true;
            foreach (var argument in node.Arguments)
            {
                if (isFirst)
                    isFirst = false;
                else
                {
                    writer.WritePunctuation(SyntaxKind.CommaToken);
                    writer.WriteSpace();
                }

                argument.WriteTo(writer);
            }
            
            writer.WritePunctuation(SyntaxKind.CloseParenthesisToken);
        }

        private static void WriteConversionExpression(BoundConversionExpression node, IndentedTextWriter writer)
        {
            writer.WriteIdentifier(node.Type.Name);
            writer.WritePunctuation(SyntaxKind.OpenParenthesisToken);
            node.Expression.WriteTo(writer);
            writer.WritePunctuation(SyntaxKind.CloseParenthesisToken);
        }

        private static void WriteBlockStatement(BoundBlockStatement node, IndentedTextWriter writer)
        {
            writer.WritePunctuation(SyntaxKind.OpenBraceToken);
            writer.WriteLine();
            writer.Indent++;

            foreach (var s in node.Statements)
                s.WriteTo(writer);
            
            writer.Indent--;
            writer.WritePunctuation(SyntaxKind.CloseBraceToken);
            writer.WriteLine();
        }

        private static void WriteExpressionStatement(BoundExpressionStatement node, IndentedTextWriter writer)
        {
            node.Expression.WriteTo(writer);
            writer.WriteLine();
        }

        private static void WriteIfStatement(BoundIfStatement node, IndentedTextWriter writer)
        {
            writer.WriteKeyword(SyntaxKind.IfKeyword);
            writer.WriteSpace();
            node.Condition.WriteTo(writer);
            writer.WriteLine();
            writer.WriteNestedStatement(node.ThenStatement);

            if (node.ElseStatement != null)
            {
                writer.WriteKeyword(SyntaxKind.ElseKeyword);
                writer.WriteLine();
                writer.WriteNestedStatement(node.ElseStatement);
            }
        }

        private static void WriteDoWhileStatement(BoundDoWhileStatement node, IndentedTextWriter writer)
        {
            writer.WriteKeyword(SyntaxKind.DoKeyword);
            writer.WriteLine();
            writer.WriteNestedStatement(node.Body);
            
            writer.WriteKeyword(SyntaxKind.WhileKeyword);
            writer.WriteSpace();
            node.Condition.WriteTo(writer);
            writer.WriteLine();
        }

        private static void WriteWhileStatement(BoundWhileStatement node, IndentedTextWriter writer)
        {
            writer.WriteKeyword(SyntaxKind.WhileKeyword);
            writer.WriteSpace();
            node.Condition.WriteTo(writer);
            writer.WriteLine();
            writer.WriteNestedStatement(node.Body);
        }

        private static void WriteForStatement(BoundForStatement node, IndentedTextWriter writer)
        {
            writer.WriteKeyword(SyntaxKind.ForKeyword);
            writer.WriteSpace();
            writer.WriteIdentifier(node.Variable.Name);
            writer.WritePunctuation(SyntaxKind.EqualsToken);
            writer.WriteSpace();
            node.LowerBound.WriteTo(writer);
            writer.WriteKeyword(SyntaxKind.ToKeyword);
            writer.WriteSpace();
            node.UpperBound.WriteTo(writer);
            writer.WriteLine();
            writer.WriteNestedStatement(node.Body);

        }

        private static void WriteVariableDeclaration(BoundVariableDeclaration node, IndentedTextWriter writer)
        {
            writer.WriteKeyword(node.Variable.IsReadOnly ? SyntaxKind.LetKeyword : SyntaxKind.ImplyKeyword );
            writer.WriteSpace();
            writer.WriteIdentifier(node.Variable.Name);
            writer.WritePunctuation(SyntaxKind.EqualsToken);
            writer.WriteSpace();
            node.Initializer.WriteTo(writer);
            writer.WriteLine();
        }

        private static void WriteGotoStatement(BoundGotoStatement node, IndentedTextWriter writer)
        {
            writer.WriteKeyword("goto ");   
            writer.WriteIdentifier(node.Label.Name);
            writer.WriteLine();
        }

        private static void WriteLabelStatement(BoundLabelStatement node, IndentedTextWriter writer)
        {
            var unindent = writer.Indent > 0;
            if (unindent)
                writer.Indent--;
            
            writer.WritePunctuation(node.Label.Name);
            writer.WriteSpace();
            writer.WritePunctuation(SyntaxKind.ColonToken);
            writer.WriteSpace();
            writer.WriteLine();
            
            if (unindent)
                writer.Indent++;
        }
        
        private static void WriteConditionalGotoStatement(BoundConditionalGotoStatement node, IndentedTextWriter writer)
        {
            writer.WriteKeyword("goto ");   
            writer.WriteIdentifier(node.Label.Name);
            writer.WriteKeyword(node.JumpIfTrue ? "if " : "unless ");   
            node.Condition.WriteTo(writer);
            writer.WriteLine();
        }
    }
}