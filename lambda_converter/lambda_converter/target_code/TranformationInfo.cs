using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lambda_converter.target_code
{
    struct TranformationInfo
    {
        SyntaxNode OriginalLambdaNode;
        ClassDeclarationSyntax ClassDeclaration;

    }
}
