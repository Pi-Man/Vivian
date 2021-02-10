﻿using System;
using System.Collections.Generic;
using Vivian.CodeAnalysis.Binding;
using Vivian.CodeAnalysis.Symbols;

namespace Vivian.CodeAnalysis
{
    internal sealed class Evaluator
    {
        private readonly BoundProgram _program;
        private readonly Dictionary<VariableSymbol, object> _globals;
        private readonly Dictionary<FunctionSymbol, BoundBlockStatement> _functions = new Dictionary<FunctionSymbol, BoundBlockStatement>();
        private readonly Stack<Dictionary<VariableSymbol, object>> _locals = new Stack<Dictionary<VariableSymbol, object>>();
        private Random _random;

        private object _lastValue;
        
        public Evaluator(BoundProgram program, Dictionary<VariableSymbol, object> variables)
        {
            _program = program;
            _globals = variables;
            _locals.Push(new Dictionary<VariableSymbol, object>());

            var current = program;
            while (current != null)
            {
                // TODO: Flagged (kv)
                foreach (var (function, body) in current.Functions)
                    _functions.Add(function, body);
                
                current = current.Previous;
            }
        }

        public object Evaluate()
        {
            var function = _program.MainFunction ?? _program.ScriptFunction;

            if (function == null)
                return null;

            var body = _functions[function];
            return EvaluateStatement(body);
        }

        private object EvaluateStatement(BoundBlockStatement body)
        {
            var labelToIndex = new Dictionary<BoundLabel, int>();

            for (var i = 0; i < body.Statements.Length; i++)
            {
                if (body.Statements[i] is BoundLabelStatement l)
                    labelToIndex.Add(l.Label, i + 1);
            }

            var index = 0;
            while (index < body.Statements.Length)
            {
                var s = body.Statements[index];
                switch (s.Kind)
                {
                    case BoundNodeKind.VariableDeclaration:
                        EvaluateVariableDeclaration((BoundVariableDeclaration) s);
                        index++;
                        break;

                    case BoundNodeKind.ExpressionStatement:
                        EvaluateExpressionStatement((BoundExpressionStatement) s);
                        index++;
                        break;

                    case BoundNodeKind.GotoStatement:
                        var gs = (BoundGotoStatement) s;
                        index = labelToIndex[gs.Label];
                        break;

                    case BoundNodeKind.ConditionalGotoStatement:
                        var cgs = (BoundConditionalGotoStatement) s;
                        var condition = (dynamic) EvaluateExpression(cgs.Condition) == 0 ? false : true;
                        if (condition == cgs.JumpIfTrue)
                            index = labelToIndex[cgs.Label];
                        else
                            index++;
                        break;

                    case BoundNodeKind.LabelStatement:
                        index++;
                        break;
                    
                    case BoundNodeKind.ReturnStatement:
                        var rs = (BoundReturnStatement) s;
                        _lastValue = rs.Expression == null ? null : EvaluateExpression(rs.Expression);
                        return _lastValue;
                    
                    default:
                        throw new Exception($"Unexpected node {s.Kind}");
                }
            }

            return _lastValue;
        }

        private void EvaluateVariableDeclaration(BoundVariableDeclaration node)
        {
            var value = EvaluateExpression(node.Initializer);
            
            _lastValue = value;
            Assign(node.Variable, value);
        }
        
        private void EvaluateExpressionStatement(BoundExpressionStatement node)
        {
            _lastValue = EvaluateExpression(node.Expression);
        }
        
        private object EvaluateExpression(BoundExpression node)
        {
            switch (node.Kind)
            {
                case BoundNodeKind.LiteralExpression:
                    return EvaluateLiteralExpression((BoundLiteralExpression) node);
                
                case BoundNodeKind.VariableExpression:
                    return EvaluateVariableExpression((BoundVariableExpression) node);
                
                case BoundNodeKind.AssignmentExpression:
                    return EvaluateAssignmentExpression((BoundAssignmentExpression) node);
                
                case BoundNodeKind.UnaryExpression:
                    return EvaluateUnaryExpression((BoundUnaryExpression) node);
                
                case BoundNodeKind.BinaryExpression:
                    return EvaluateBinaryExpression((BoundBinaryExpression) node);
                
                case BoundNodeKind.CallExpression:
                    return EvaluateCallExpression((BoundCallExpression) node);
                
                case BoundNodeKind.ConversionExpression:
                    return EvaluateConversionExpression((BoundConversionExpression) node);
                
                default:
                    throw new Exception($"Unexpected node {node.Kind}");
            }
        }

        private object EvaluateConversionExpression(BoundConversionExpression node)
        {
            var value = EvaluateExpression(node.Expression);
            if (node.Type == TypeSymbol.Object)
                return value;
            if ((node.Type.Caps & TypeSymbolCaps.Arithmetic) != TypeSymbolCaps.None)
            {
                if (value is string s)
                {
                    return s.Equals("true") ?
                        1 :
                        s.Equals("false") ?
                        0 :
                        throw new Exception($"Value {value} can not be cast to type {node.Type}");
                }
                return Conversion.Convert(node.Type, value);
            }
            else if (node.Type == TypeSymbol.String)
            {
                if (node.Expression.Type == TypeSymbol.Bool)
                {
                    return (byte)value == 0 ? "false" : "true";
                }
                return Convert.ToString(value);
            }
            else
                throw new Exception($"Unexpected type {node.Type}");
        }
        
        private static object EvaluateLiteralExpression(BoundLiteralExpression n)
        {
            return n.Value;
        }
        
        private object EvaluateVariableExpression(BoundVariableExpression v)
        {
            if (v.Variable.Kind == SymbolKind.GlobalVariable)
            {
                return _globals[v.Variable];
            }
            else
            {
                var locals = _locals.Peek();
                return locals[v.Variable];
            }
        }
        
        private object EvaluateAssignmentExpression(BoundAssignmentExpression a)
        {
            
            var value = EvaluateExpression(a.Expression);
            Assign(a.Variable, value);

            return value;
        }

        private void Assign(VariableSymbol variable, object value)
        {
            if (variable.Kind == SymbolKind.GlobalVariable)
            {
                _globals[variable] = value;
            }
            else
            {
                var locals = _locals.Peek();
                locals[variable] = value;
            }
        }

        private object EvaluateUnaryExpression(BoundUnaryExpression u)
        {
            var operand = EvaluateExpression(u.Operand);
            var conversion = Conversion.Classify(u.Operand.Type, u.Type);
            if (!conversion.IsImplicit)
                throw new Exception($"Can not implicitly cast type {u.Operand.Type} to {u.Type}");

            return Conversion.Convert(u.Type, u.Op.Operate(Conversion.Convert(u.Type, operand))); // Convert twice incase C# changes the type after the operation
        }
        
        private object EvaluateBinaryExpression(BoundBinaryExpression b)
        {
            var left = EvaluateExpression(b.Left);
            var right = EvaluateExpression(b.Right);

            if (b.Op.Type == null)
            {
                var conversionL = Conversion.Classify(b.Left.Type, b.Type);
                if (!conversionL.IsImplicit)
                    throw new Exception($"Can not implicitly cast type {b.Left.Type} to {b.Type}");

                var conversionR = Conversion.Classify(b.Right.Type, b.Type);
                if (!conversionR.IsImplicit)
                    throw new Exception($"Can not implicitly cast type {b.Right.Type} to {b.Type}");

                left = Conversion.Convert(b.Type, left);
                right = Conversion.Convert(b.Type, right);
            }

            return Conversion.Convert(b.Type, b.Op.Operate(left, right));

            //switch (b.Op.Kind)
            //{
            //    case BoundBinaryOperatorKind.Addition:
            //        if ((b.Type.Caps & TypeSymbolCaps.Arithmetic) != TypeSymbolCaps.None)
            //        {
            //            var conversionL = Conversion.Classify(b.Left.Type, b.Type);
            //            if (!conversionL.IsImplicit)
            //                throw new Exception($"Can not implicitly cast type {b.Left.Type} to {b.Type}");

            //            var conversionR = Conversion.Classify(b.Right.Type, b.Type);
            //            if (!conversionR.IsImplicit)
            //                throw new Exception($"Can not implicitly cast type {b.Right.Type} to {b.Type}");

            //            left = Conversion.Convert(b.Type, left);
            //            right = Conversion.Convert(b.Type, right);

            //            var value = (dynamic)left + (dynamic)right;

            //            return Conversion.Convert(b.Type, value);
            //        }
            //        else
            //            return (string)left + (string)right;

            //    case BoundBinaryOperatorKind.Subtraction:
            //        return (int)left - (int) right;

            //    case BoundBinaryOperatorKind.Multiplication:
            //        return (int)left * (int) right;

            //    case BoundBinaryOperatorKind.Division:
            //        return (int)left / (int) right;

            //    case BoundBinaryOperatorKind.Modulo:
            //        return (int)left % (int) right;

            //    case BoundBinaryOperatorKind.BitwiseAnd:
            //        return (int)left & (int) right;

            //    case BoundBinaryOperatorKind.BitwiseOr:
            //        return (int)left | (int) right;

            //    case BoundBinaryOperatorKind.BitwiseXor:
            //        return (int)left ^ (int) right;

            //    case BoundBinaryOperatorKind.LogicalAnd:
            //        return ((int) left) == 0 ? 0 : ((int) right) == 0 ? 0 : 1;

            //    case BoundBinaryOperatorKind.LogicalOr:
            //        return ((int) left) == 0 ? ((int) right) == 0 ? 0 : 1 : 1;

            //    case BoundBinaryOperatorKind.Equals:
            //        return Equals(left, right) ? 1 : 0;

            //    case BoundBinaryOperatorKind.NotEquals:
            //        return !Equals(left, right) ? 1 : 0;

            //    case BoundBinaryOperatorKind.Less:
            //        return (int) left < (int) right ? 1 : 0;

            //    case BoundBinaryOperatorKind.Greater:
            //        return (int) left > (int) right ? 1 : 0;

            //    case BoundBinaryOperatorKind.GreaterOrEquals:
            //        return (int) left >= (int) right ? 1 : 0;

            //    case BoundBinaryOperatorKind.LessOrEquals:
            //        return (int) left <= (int) right ? 1 : 0;

            //    default:
            //        throw new Exception($"Unexpected binary operator {b.Op}");
            //}
        }

        private object EvaluateCallExpression(BoundCallExpression node)
        {
            if (node.Function == BuiltinFunctions.Input)
            {
                return Console.ReadLine();
            }
            
            else if (node.Function == BuiltinFunctions.Print)
            {
                var message = (object) EvaluateExpression(node.Arguments[0]);
                Console.WriteLine(message);
                return null;
            }
            else if (node.Function == BuiltinFunctions.Rnd)
            {
                var max = (int) EvaluateExpression(node.Arguments[0]);
                if (_random == null)
                    _random = new Random();

                return _random.Next(max);
            }
            else
            {
                var locals = new Dictionary<VariableSymbol, object>();
                for (var i = 0; i < node.Arguments.Length; i++)
                {
                    var parameter = node.Function.Parameters[i];
                    var value = EvaluateExpression(node.Arguments[i]);
                    locals.Add(parameter, value);
                }

                _locals.Push(locals);

                var statement = _functions[node.Function];
                var result =  EvaluateStatement(statement);
                
                _locals.Pop();
                return result;
            }
        }
    }
}