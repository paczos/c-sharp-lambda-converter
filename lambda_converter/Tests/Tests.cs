using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static lambda_converter.LambdaConverter;
using lambda_converter;

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
    }
}
