using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lambda_converter
{
    class TransformationInfo
    {
        public SyntaxNode OriginalLambdaNode;
        public ClassDeclarationSyntax ClassDeclaration;
        public LocalDeclarationStatementSyntax InstanceInitSyntax;
        public List<ExpressionStatementSyntax> StatementBeforeLambdaExpression;
        public ExpressionSyntax MethodUsage;
    }
}
