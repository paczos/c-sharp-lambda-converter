using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

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

        static string CODELOCATION = ".\\targetCode\\LambdaCode.cs";
        const string RESULTLOCATION = ".\\targetCode\\NonLambdaCode.cs";

        static int instanceIndex = 0;
        static int classIndex = 0;
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
            List<TransformationInfo> transformations = new List<TransformationInfo>();

            foreach (var l in lambdas)
            {
                var transInfo = new TransformationInfo();
                transInfo.OriginalLambdaNode = root.DescendantNodes().First(m => m == l);


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

                        var className = "lambdClass" + classIndex++;
                        var instanceName = "lambdInst" + instanceIndex++;
                        var methodName = "methodLambd";

                        var methodDef = SyntaxFactory.MethodDeclaration(parsedReturntype, methodName)
                        .WithParameterList(SyntaxFactory.ParseParameterList(paramsListString))
                        .WithBody(lambdaBody).WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword))).NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

                        var fields = captured.Where(m=> (m as ILocalSymbol)!=null).Select(m =>
                        {
                            var sym = (m as ILocalSymbol);

                            var type = SyntaxFactory.ParseTypeName(sym?.Type?.ToDisplayString());
                            var name = sym?.Name;
                            return SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(type).WithVariables(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(name))))).WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)));
                        });

                        var methods = SyntaxFactory.SingletonList<MemberDeclarationSyntax>(methodDef);
                        SyntaxList<MemberDeclarationSyntax> members = SyntaxFactory.List(fields.Union(methods));

                        var classDecl = SyntaxFactory.ClassDeclaration(className)
                                                     .WithMembers(members)
                                                     .NormalizeWhitespace()
                                                     .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

                        transInfo.ClassDeclaration = classDecl;

                        //instantiation and field filling

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
                             .NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine("\n")); ;

                        //fill each capturing field
                        var capturingFieldsAssignments = captured.Where(m=> (m as ILocalSymbol)!=null).Select(m =>
                        {
                            var sym = (m as ILocalSymbol);
                            var type = SyntaxFactory.ParseTypeName(sym.Type.ToDisplayString());
                            var name = sym.Name;
                            var capturingFieldAssignment = SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName(instanceName), SyntaxFactory.IdentifierName(name)), SyntaxFactory.IdentifierName(name)));

                            return capturingFieldAssignment.NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine("\n")); ;
                        });

                        transInfo.InstanceInitSyntax = instanceSyntax;
                        transInfo.StatementBeforeLambdaExpression = capturingFieldsAssignments.ToList();

                        //method call
                        var method = SyntaxFactory.ParseExpression(instanceName + "." + methodDef.Identifier.ToFullString());

                        transInfo.MethodUsage = method;

                        transformations.Add(transInfo);
                    }
                }
            }
            // https://joshvarty.wordpress.com/2015/08/18/learn-roslyn-now-part-12-the-documenteditor/
            var documentEditor = DocumentEditor.CreateAsync(document).Result;


            var firstChild = root.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>().First().DescendantNodes().First();

            //transform code
            foreach (var trans in transformations)
            {
                documentEditor.InsertAfter(firstChild, trans.ClassDeclaration);
                var statements = trans.OriginalLambdaNode.Ancestors().OfType<BlockSyntax>().FirstOrDefault().ChildNodes()
                    .OfType<LocalDeclarationStatementSyntax>().ToList<SyntaxNode>().Union(trans.OriginalLambdaNode.Ancestors()
                    .OfType<ExpressionStatementSyntax>().ToList<SyntaxNode>()).ToList();

                var ancestors = trans.OriginalLambdaNode.Ancestors()
                    .OfType<LocalDeclarationStatementSyntax>().ToList<SyntaxNode>()
                    .Union(trans.OriginalLambdaNode.Ancestors()
                    .OfType<ExpressionStatementSyntax>().ToList());
                var index = statements.IndexOf(ancestors.FirstOrDefault());

                var prevStatement = statements.ElementAtOrDefault(index);

                if (prevStatement != null)
                    documentEditor.InsertBefore(prevStatement, (new List<SyntaxNode> { trans.InstanceInitSyntax }).Union(trans.StatementBeforeLambdaExpression));

                documentEditor.ReplaceNode(trans.OriginalLambdaNode, trans.MethodUsage);
            }

            var updatedDoc = Formatter.FormatAsync(documentEditor.GetChangedDocument()).Result;
            string resultCode = updatedDoc.GetSyntaxTreeAsync().Result.ToString();
            string[] resultLines = resultCode.Split('\n');
            Console.WriteLine();
            using(StreamWriter sr = new StreamWriter(RESULTLOCATION))
            {
                foreach(var l in resultLines)
                {
                    sr.WriteLine(l);
                }
            }
        }
    }
}