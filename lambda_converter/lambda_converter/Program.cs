using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace lambda_converter
{

    class Program
    {

        static bool IsLambda(SyntaxNode node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.ParenthesizedLambdaExpression:
                case SyntaxKind.SimpleLambdaExpression:
                case SyntaxKind.AnonymousMethodExpression:
                case SyntaxKind.LetClause:
                case SyntaxKind.WhereClause:
                case SyntaxKind.AscendingOrdering:
                case SyntaxKind.DescendingOrdering:
                case SyntaxKind.JoinClause:
                case SyntaxKind.GroupClause:
                case SyntaxKind.LocalFunctionStatement:
                    return true;

                case SyntaxKind.FromClause:
                    // The first from clause of a query expression is not a lambda.
                    return !node.Parent.IsKind(SyntaxKind.QueryExpression);
            }

            return false;
        }

        static string CODELOCATION = ".\\target_code\\LambdaCode.cs";
        static int Main(string[] args)
        {
            string code;

            using (StreamReader sr = new StreamReader(CODELOCATION))
            {
                code = sr.ReadToEnd();

            }

            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            var lambdas = root.DescendantNodes().Where(m => IsLambda(m) == true);
            
            foreach( var l in lambdas)
            {
                Console.WriteLine(l.ToString());
            }

            return 0;
        }

        private static IEnumerable<object> SimpleLambdaExpression()
        {
            throw new NotImplementedException();
        }
    }
}
