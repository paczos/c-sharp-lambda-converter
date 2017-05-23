using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static lambda_converter.LambdaConverter;
using lambda_converter;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace LambdaConverterTests
{
    [TestClass]
    public class TestErrorHandling
    {
        [TestMethod]
        [ExpectedException(typeof(UnsupportedCodeTransformationException),
        "ClassField was captured")]
        public void CheckClassFieldDetection()
        {
            string code = @"
        using System;

        class LambdaCode
        {
            int a = 3;

            void meth(){
                Func<int, int> fieldLambd = (m)=>(m+a);
                }
        }";

            LambdaConverter.Convert(code);
        }


        [TestMethod]
        [ExpectedException(typeof(ImproperInputCodeException), "Improper C# code")]
        public void TestCodeThatDoesNotCompile()
        {
            string code = "ajsdniasndkasnkjasa88 * ^& 1 void s()";
            Convert(code);
        }
        [TestMethod]
        [ExpectedException(typeof(NotSupportedException), "This code cannot be converted properly")]
        public void TestRecursiveLambdaConversion()
        {
            string code = @"
        class SomeClass
        {
            public void Method()
            {
                Func<Func<int, int>, Func<int, int>> factorial = (fac) => x => x == 0 ? 0 : x * fac(x - 1); 

            }
        }
        ";
            var res = LambdaConverter.Convert(code);
        }


    }
    [TestClass]
    public class TestLambdaConversions
    {
        private IEnumerable<SyntaxNode> GetLambdas(string code)
        {
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
            return root.DescendantNodes().Where(m => isLambda(m) == true).ToList();
        }

        [TestMethod]
        public void TestSimpleLambdaConversion()
        {
            string code = @"
        class SomeClass
        {
            public void Method()
            {
            //SimpleLambdaExpression
            var even = ints.Where(m => m % 2 == 0).ToList();
            }
        }
        ";
            var res = LambdaConverter.Convert(code);

            Assert.IsTrue(GetLambdas(res).Count() == 0);
        }

        [TestMethod]
        public void TestParenthesisedLambdaConversion()
        {
            string code = @"
        class SomeClass
        {
            public void Method()
            {
              int[] localInts = { 3, 6, 9 };
            //ParenthesisedLambdaExpression
            var zipped = localInts.Zip(externalInts, (int m, int n) => { return m - n; }).ToList();

            }
        }
        ";
            var res = LambdaConverter.Convert(code);

            Assert.IsTrue(GetLambdas(res).Count() == 0);
        }

        [TestMethod]
        public void TestCaptureLambdaConversion()
        {
            string code = @"
        class SomeClass
        {
            public void Method()
            {
            string text = ""result of zipping"";
            string teee = ""some random text"";
            int abba = 123;
            //statement lambda with capture
            zipped.ForEach((n) =>
            {
                Console.WriteLine(text);
                Console.WriteLine(n);
                Console.WriteLine(teee + 3 * abba + text);
            });

            }
        }";
            var res = LambdaConverter.Convert(code);

            Assert.IsTrue(GetLambdas(res).Count() == 0);
        }

        [TestMethod]
        public void TestVoidLambdaConversion()
        {
            string code = @"
        class SomeClass
        {
            public void Method()
            {
            Func<int> voidLam = () => 3;
            }
        }
        ";
            var res = LambdaConverter.Convert(code);

            Assert.IsTrue(GetLambdas(res).Count() == 0);
        }

        [TestMethod]
        public void TestCustomValueTypesAsParameters()
        {
            string code = @"
        class SomeClass
        {
        private class UserDefinedStruct
        {
            public int a;
        }
            public void Method()
            {
            //Check if custom value types converted properly
            Func<UserDefinedStruct, int> custStructLambda = (UserDefinedStruct b) => b.a - 1;
            }
        }
        ";
            var res = LambdaConverter.Convert(code);

            Assert.IsTrue(GetLambdas(res).Count() == 0);
        }

        [TestMethod]
        public void TestCustomReferenceTypesAsParameters()
        {
            string code = @"
        class SomeClass
        {
        class UserDefinedType
        {
            public int a;
            float c;
        }

            public void Method()
            {
            //Check if custom value types converted properly
            Func<UserDefinedType, int> custTypeLambda = (UserDefinedType a) => a.a;

            }
        }
        ";
            var res = LambdaConverter.Convert(code);

            Assert.IsTrue(GetLambdas(res).Count() == 0);
        }
        //Func<int,Func<int>> nested = (b) => () => b*3;
        [TestMethod]
        [ExpectedException(typeof(NotSupportedException), "This code cannot be converted properly")]
        public void TestNestedLambdaConversion()
        {
            string code = @"
        class SomeClass
        {
            public void Method()
            {
                Func<int,Func<int>> nested = (b) => () => b*3;
            }
        }
        ";
            var res = LambdaConverter.Convert(code);
        }


        [TestMethod]
        public void TestDoubleConversion()
        {
            //input -> code that had already been converted, had new lambda added and then sent to this program
            string code = @"
    using System;    
    class ExternalLambdaCode
    {
        static void main()
        {
            lambdClass1 lambdInst1 = new lambdClass1();
            //
            // Use implicitly-typed lambda expression.
            // ... Assign it to a Func instance.
            //
            Func<int, int> func1 = lambdInst1.methodLambd;
            
            //NEW LAMBDA
            Func<int, int> func2 = m => m+10;

            Console.WriteLine(func1(1));

        }
        class lambdClass1
        {
            public int methodLambd(Int32 x)
            {
                return x + 1;
            }
        }
    }
        ";
            var res = LambdaConverter.Convert(code);

            Assert.IsTrue(GetLambdas(res).Count() == 0);
        }
    }
}
