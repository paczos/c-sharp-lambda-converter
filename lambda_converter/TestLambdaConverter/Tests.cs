using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

namespace TestLambdaConverter
{
    [TestClass]
    public class TestErrorHandling
    {
        [TestMethod]
        public void CheckClassFieldDetection()
        {
            string code = @"
        class LambdaCode
        {
            int a = 3;

            void meth(){
                Func<int, int> fieldLambd = (m)=>(m+a);
                }
        }";

            var sut = new LambdaConverter();
            Assert.ThrowsException<ClassFieldCaptureException>
        }

    }
    [TestClass]
    public class TestCodeConversion
    {
        [TestMethod]
        public void TestMethod1()
        {
        }
    }

}
