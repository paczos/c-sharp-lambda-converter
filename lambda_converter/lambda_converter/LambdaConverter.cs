using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace lambda_converter
{
    public class LambdaConverter
    {
        public class UnsupportedCodeTransformationException : Exception
        {
            public UnsupportedCodeTransformationException(string message) : base(message)
            {
            }
        }
        public class ImproperInputCodeException : Exception
        {
            public ImproperInputCodeException(string message) : base(message)
            {
            }
        }

        public static bool isLambda(SyntaxNode node)
        {
            //all kinds of syntaxnodes that are represented by lambda expressions
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

        //functionality tests, code
        //unique class names
        //printed final report

        const string LAMBDA_CLASS_BASENAME = "lambdClass";
        const string LAMBDA_METHOD_BASENAME = "methodLambd";
        const string LAMBDA_CLASS_INSTANCE_BASENAME = "lambdInst";
        static int classIndex = 0;

        public static string Convert(string code)
        {
            var workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId();
            var versionStamp = VersionStamp.Create();
            var projectInfo = ProjectInfo.Create(projectId,
                                                versionStamp,
                                                "LambdNewProject",
                                                "labdaProjName",
                                                LanguageNames.CSharp);
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

            var lambdas = root.DescendantNodes().Where(m => isLambda(m) == true).ToList();

            Dictionary<SyntaxNode, SyntaxNode> replacement = new Dictionary<SyntaxNode, SyntaxNode>();
            List<TransformationInfo> transformations = new List<TransformationInfo>();

            foreach (var lambda in lambdas)
            {
                var transInfo = new TransformationInfo();
                transInfo.OriginalLambdaNode = root.DescendantNodes().First(m => m == lambda);

                var symbol = semantic.GetSymbolInfo(lambda).Symbol;
                var methodSymbol = symbol as IMethodSymbol;


                if (methodSymbol == null)
                {
                    continue;
                }
                if (methodSymbol.ReturnType != null)
                {
                    var returntype = methodSymbol.ReturnType as TypeSyntax;
                    var parsedReturntype = SyntaxFactory.ParseTypeName(methodSymbol.ReturnType.ToDisplayString());

                    var lambdaExpression = lambda as LambdaExpressionSyntax;

                    if (lambdaExpression != null)
                    {
                        BlockSyntax lambdaBody;


                        DataFlowAnalysis result = semantic.AnalyzeDataFlow(lambdaExpression);

                        var captured = result.DataFlowsIn;
                        var parentClass = lambda.Ancestors().OfType<ClassDeclarationSyntax>().First();
                        var parentClassFields = semantic.LookupSymbols(parentClass.ChildNodes()
                            .OfType<MethodDeclarationSyntax>().First().SpanStart).OfType<IFieldSymbol>();
                        var lambdaFields = semantic.LookupSymbols(lambda.SpanStart).OfType<ILocalSymbol>();

                        var paramsListString = "(" + string.Join(", ", methodSymbol.Parameters.Select(m => m.Type.Name + " " + m.Name)) + ")";


                        if (methodSymbol.ReturnType.SpecialType == SpecialType.System_Void)
                        {
                            //simply copy lambda body
                            lambdaBody = SyntaxFactory.Block(
                                lambdaExpression.Body.DescendantNodes().OfType<StatementSyntax>());

                        }
                        else
                        {
                            ExpressionSyntax expr = SyntaxFactory.ParseExpression(lambdaExpression.Body.ToFullString());
                            if (lambdaExpression.DescendantNodes().OfType<ReturnStatementSyntax>().Any())
                            {
                                //do not insert return statement as it is already present
                                lambdaBody = SyntaxFactory.Block(lambdaExpression.Body.DescendantNodes()
                                    .OfType<StatementSyntax>());
                            }
                            else
                            {
                                //add return statement
                                lambdaBody = SyntaxFactory.Block(SyntaxFactory.ReturnStatement(expr));
                            }
                        }

                        var className = GetNextClassName();

                        while (semantic.LookupSymbols(lambdaExpression.SpanStart).Select(m => m.Name).Contains(className))
                        {
                            className = GetNextClassName();
                        }

                        var instanceName = LAMBDA_CLASS_INSTANCE_BASENAME + classIndex;

                        var methodDef = SyntaxFactory.MethodDeclaration(parsedReturntype, LAMBDA_METHOD_BASENAME)
                        .WithParameterList(SyntaxFactory.ParseParameterList(paramsListString))
                        .WithBody(lambdaBody).WithModifiers(SyntaxFactory
                            .TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                            .NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));


                        var fields = captured.Where(m => (m as ILocalSymbol) != null).Select(m =>
                        {
                            var sym = (m as ILocalSymbol);
                            var type = SyntaxFactory.ParseTypeName(sym?.Type?.ToDisplayString());
                            var name = sym?.Name;
                            return SyntaxFactory.FieldDeclaration(
                                SyntaxFactory.VariableDeclaration(type)
                                .WithVariables(SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(name)))))
                                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory
                                    .Token(SyntaxKind.PublicKeyword)));
                        });


                        if (captured.Where(m => (m as IParameterSymbol) != null).Any())
                        {
                            throw new UnsupportedCodeTransformationException("Cannot convert lambdas that refer to class fields using this keyword. Is this keyword neccessary?");
                        }

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
                        var capturingFieldsAssignments = captured.Where(m => (m as ILocalSymbol) != null).Select(m =>
                        {
                            var sym = (m as ILocalSymbol);
                            var type = SyntaxFactory.ParseTypeName(sym.Type.ToDisplayString());
                            var name = sym.Name;
                            var capturingFieldAssignment = SyntaxFactory.ExpressionStatement(SyntaxFactory
                                .AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName(instanceName),
                                    SyntaxFactory.IdentifierName(name)), SyntaxFactory.IdentifierName(name)));

                            return capturingFieldAssignment.NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine("")); ;
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

            var firstChild = root.DescendantNodesAndSelf()
                                    .OfType<ClassDeclarationSyntax>()
                                    .FirstOrDefault()?.DescendantNodes()?.FirstOrDefault();

            if (firstChild == null)
                throw new ImproperInputCodeException("Supplied code is an improper C# code. No parent class.");

            //transform code
            foreach (var trans in transformations)
            {
                documentEditor.InsertAfter(firstChild, trans.ClassDeclaration);
                var statements = trans.OriginalLambdaNode.Ancestors()
                    .OfType<BlockSyntax>().FirstOrDefault().ChildNodes()
                        .OfType<LocalDeclarationStatementSyntax>().ToList<SyntaxNode>()
                        .Union(trans.OriginalLambdaNode.Ancestors()
                        .OfType<ExpressionStatementSyntax>().ToList<SyntaxNode>()).ToList();

                var ancestors = trans.OriginalLambdaNode.Ancestors()
                    .OfType<LocalDeclarationStatementSyntax>().ToList<SyntaxNode>()
                    .Union(trans.OriginalLambdaNode.Ancestors()
                    .OfType<ExpressionStatementSyntax>().ToList());
                var index = statements.IndexOf(ancestors.FirstOrDefault());

                var prevStatement = statements.ElementAtOrDefault(index);

                if (prevStatement != null)
                    documentEditor.InsertBefore(prevStatement, (new List<SyntaxNode> {
                        trans.InstanceInitSyntax
                    }).Union(trans.StatementBeforeLambdaExpression));

                documentEditor.ReplaceNode(trans.OriginalLambdaNode, trans.MethodUsage);
            }

            try
            {
                var updatedDoc = Formatter.FormatAsync(documentEditor.GetChangedDocument()).Result;
                string resultCode = updatedDoc.GetSyntaxTreeAsync().Result.ToString();
                return resultCode;
            }
            catch (IOException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new NotSupportedException("Unexpected transformation error occured while generating final output: " + ex.ToString());
            }
        }

        private static string GetNextClassName()
        {
            return LAMBDA_CLASS_BASENAME + ++classIndex;
        }
    }
}
