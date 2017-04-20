using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Editing;

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
        static int delegateIndex = 0;
        static void Main(string[] args)
        {
            string code;

            using (StreamReader sr = new StreamReader(CODELOCATION))
            {
                code = sr.ReadToEnd();

            }

            var workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId();
            var versionStamp = VersionStamp.Create();
            var projectInfo = ProjectInfo.Create(projectId, versionStamp, "LambdNewProject", "labdaProjName", LanguageNames.CSharp);
            var newProject = workspace.AddProject(projectInfo);
            var document = workspace.AddDocument(newProject.Id, "NewFile.cs", SourceText.From(code));


            var tree = document.GetSyntaxTreeAsync().Result;
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
            List<SyntaxNode> classesDefinitionsToBeInserted = new List<SyntaxNode>();

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
                        var captured = result.DataFlowsIn;
                        var capturedString = captured.Select(m =>
                        {
                            var s = m as ILocalSymbol;
                            return s.Type.Name + " " + s.Name;

                        });



                        var paramsListString = "(" + string.Join(", ", methodSymbol.Parameters.Select(m => m.Type.Name + " " + m.Name)) + ")";

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
                                //do not insert return as it is already present
                                lambdaBody = SyntaxFactory.Block(lambdCast.Body.DescendantNodes().OfType<StatementSyntax>());
                            }
                            else
                            {
                                //add return statement
                                lambdaBody = SyntaxFactory.Block(SyntaxFactory.ReturnStatement(expr));
                            }
                        }

                        var instanceInitSyntax = SyntaxFactory.List<StatementSyntax>();

                        var methodDef = SyntaxFactory.MethodDeclaration(parsedReturntype, "method" + delegateIndex++)
                        .WithParameterList(SyntaxFactory.ParseParameterList(paramsListString))
                        .WithBody(lambdaBody).NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

                        var fields = captured.Select(m =>
                        {
                            var sym = (m as ILocalSymbol);
                            var type = SyntaxFactory.ParseTypeName(sym.Type.ToDisplayString());
                            var name = sym.Name;
                            return SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(type).WithVariables(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(name)))));
                        });

                        var methods = SyntaxFactory.SingletonList<MemberDeclarationSyntax>(methodDef);
                        SyntaxList<MemberDeclarationSyntax> members = SyntaxFactory.List(fields.Union(methods));

                        var classDecl = SyntaxFactory.ClassDeclaration("lambd")
                                                     .WithMembers(members)
                                                     .NormalizeWhitespace()
                                                     .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));


                        classesDefinitionsToBeInserted.Add(classDecl);
                        methodsDefinitionsToBeInserted.Add(methodDef);

                        //instantiation and field filling

                        var className = "lambdClass";
                        var instanceName = "lambdInst";
                        var methodName = "methodLambd";
                        var instanceSyntax = SyntaxFactory.LocalDeclarationStatement(
                            SyntaxFactory.VariableDeclaration(
                                SyntaxFactory.IdentifierName(
                                    SyntaxFactory.Identifier(className)))
                                    .WithVariables(SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.VariableDeclarator(
                                            SyntaxFactory.Identifier(instanceName))
                                            .WithInitializer(SyntaxFactory
                                            .EqualsValueClause(SyntaxFactory
                                            .ObjectCreationExpression(SyntaxFactory.IdentifierName(className))
                                            .WithArgumentList(SyntaxFactory.ArgumentList()))))))
                             .NormalizeWhitespace();

                        //fill each capturing field
                        var capturingFieldsAssignments = captured.Select(m =>
                        {
                            var sym = (m as ILocalSymbol);
                            var type = SyntaxFactory.ParseTypeName(sym.Type.ToDisplayString());
                            var name = sym.Name;
                            var capturingFieldAssignment = SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName(instanceName), SyntaxFactory.IdentifierName(name)), SyntaxFactory.IdentifierName(name)));

                            return capturingFieldAssignment;
                        });
                        Console.WriteLine(capturingFieldsAssignments.Count());

                        //method call
                        var method = SyntaxFactory.ParseExpression(methodDef.Identifier.ToFullString());

                        replacement[l] = method;
                    }
                }
            }
            // https://joshvarty.wordpress.com/2015/08/18/learn-roslyn-now-part-12-the-documenteditor/
            var documentEditor = DocumentEditor.CreateAsync(document).Result;


            // documentEditor.ReplaceNodes(replacement.Keys, (n, m) => replacement[n]);

            var firstChild = root.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>().First().DescendantNodes().First();

            documentEditor.InsertAfter(firstChild, classesDefinitionsToBeInserted);

            foreach (var n in replacement.Keys)
            {

                documentEditor.ReplaceNode(n, replacement[n]);
            }
            var updatedDoc = documentEditor.GetChangedDocument();



            //TODO insert lambd  classes instances creation with field initializaions

            Console.WriteLine(updatedDoc.GetSyntaxTreeAsync().Result.ToString());
        }
    }
}