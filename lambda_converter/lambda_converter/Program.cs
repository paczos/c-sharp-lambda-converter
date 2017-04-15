using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


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
                .AddSyntaxTrees(tree)
                .WithOptions(options);
            var semantic = comp.GetSemanticModel(tree);

            var lambdas = root.DescendantNodes().Where(m => IsLambda(m) == true).ToList();


            Dictionary<SyntaxNode, SyntaxNode> replacement = new Dictionary<SyntaxNode, SyntaxNode>();
            List<SyntaxNode> methodsDefinitionsToBeInserted = new List<SyntaxNode>();
            foreach (var l in lambdas)
            {
                var methodSymbol = semantic.GetSymbolInfo(l).Symbol as IMethodSymbol;


                if (methodSymbol == null)
                {
                    continue;
                }
                if (methodSymbol.ReturnType != null)
                {
                    var returntype = methodSymbol.ReturnType as TypeSyntax;
                    var parsedReturntype = SyntaxFactory.ParseTypeName(methodSymbol.ReturnType.ToDisplayString());

                    var lambdCast = l as LambdaExpressionSyntax;

                    if (lambdCast != null)
                    {
                        BlockSyntax lambdaBody;


                        DataFlowAnalysis result = semantic.AnalyzeDataFlow(lambdCast);
                        var capturedString = result.DataFlowsIn.Select(m =>
                        {
                            var s = m as ILocalSymbol;
                            return s.Type.Name + " " + s.Name;

                        });



                        var paramsListString = "(" + string.Join(", ", methodSymbol.Parameters.Select(m => m.Type.Name + " " + m.Name).Union(capturedString)) + ")";

                        if (methodSymbol.ReturnType.SpecialType == SpecialType.System_Void)
                        {
                            //simply copy lambda body
                            lambdaBody = SyntaxFactory.Block(lambdCast.Body.DescendantNodes().OfType<StatementSyntax>());

                        }
                        else
                        {
                            ExpressionSyntax expr = SyntaxFactory.ParseExpression(lambdCast.Body.ToFullString());
                            if (lambdCast.DescendantNodes().OfType<ReturnStatementSyntax>().Any())
                            {
                                //do not insery return as it is already present
                                lambdaBody = SyntaxFactory.Block(lambdCast.Body.DescendantNodes().OfType<StatementSyntax>());
                            }
                            else
                            {
                                //add return kword
                                lambdaBody = SyntaxFactory.Block(SyntaxFactory.ReturnStatement(expr));
                            }
                        }

                        var methodDef = SyntaxFactory.MethodDeclaration(parsedReturntype, "method" + delegateIndex++)
                        .WithParameterList(SyntaxFactory.ParseParameterList(paramsListString))
                        .WithBody(lambdaBody).NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

                        methodsDefinitionsToBeInserted.Add(methodDef);

                        //method call
                        var method = SyntaxFactory.ParseExpression(methodDef.Identifier.ToFullString());

                        replacement[l] = method;
                    }
                }
            }

            root = root.ReplaceNodes(replacement.Keys, (n, m) => replacement[n]);
            var firstChild = root.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>().First().DescendantNodes().First();
            root = root.InsertNodesAfter(firstChild, methodsDefinitionsToBeInserted);

            Console.WriteLine(root.SyntaxTree.ToString());
            return 0;
        }
    }
}