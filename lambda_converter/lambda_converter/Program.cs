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
        static string delegateNameBase = "__generatedDelegate";
        static int delegateIndex = 0;
        static int Main(string[] args)
        {
            string code;

            using (StreamReader sr = new StreamReader(CODELOCATION))
            {
                code = sr.ReadToEnd();

            }

            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            var systemCore = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            var comp = CSharpCompilation.Create("LambdaCode")
                .AddReferences(mscorlib, systemCore)
                .AddSyntaxTrees(tree).WithOptions(options);
            var semantic = comp.GetSemanticModel(tree);

            var lambdas = root.DescendantNodes().Where(m => IsLambda(m) == true).ToList();


            Dictionary<SyntaxNode, SyntaxNode> replacement = new Dictionary<SyntaxNode, SyntaxNode>();
            List<SyntaxNode> toBeInserted = new List<SyntaxNode>();
            foreach (var l in lambdas)
            {
                var methodSymbol = semantic.GetSymbolInfo(l).Symbol as IMethodSymbol;


                if (methodSymbol == null)
                {
                    continue;
                }

                if (methodSymbol.ReturnType != null)
                {
                    Console.WriteLine("Lambda: " + l.ToString() + " has return type " +
                        methodSymbol.ReturnType + " and parameters' types: " +
                        string.Join(" ", methodSymbol.Parameters.Select(m => m.Type.ToString())));


                    var returntype = methodSymbol.ReturnType as TypeSyntax;
                    var parsedReturntype = SyntaxFactory.ParseTypeName(methodSymbol.ReturnType.ToDisplayString());

                    var lambdCast = l as ParenthesizedLambdaExpressionSyntax;

                    if (lambdCast != null)
                    {
                        var paramsList = "(" + string.Join(", ", methodSymbol.Parameters.Zip(methodSymbol.Parameters, (m, n) => m.Type + " " + n.Name)) + ")";

                        var methodDef = SyntaxFactory.MethodDeclaration(parsedReturntype, "method" + delegateIndex++)
                        .WithParameterList(SyntaxFactory.ParseParameterList(paramsList))
                        .WithBody(SyntaxFactory.Block(SyntaxFactory.ReturnStatement(SyntaxFactory.ParseExpression(lambdCast.Body.ToFullString())))).NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));
                        
                        toBeInserted.Add(methodDef);

                        var str = methodDef.ToFullString();

                        //method call
                        var method = SyntaxFactory.ParseExpression(methodDef.Identifier.ToFullString());

                        replacement[l] = method;


                    }

                }
            }
            root = root.ReplaceNodes(replacement.Keys, (n, m) => replacement[n]);
            var firstChild = root.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>().First().DescendantNodes().First();
            root = root.InsertNodesAfter(firstChild, toBeInserted);




            Console.WriteLine(root.SyntaxTree.ToString());

            return 0;
        }



        private static IEnumerable<object> SimpleLambdaExpression()
        {
            throw new NotImplementedException();
        }
    }
}
