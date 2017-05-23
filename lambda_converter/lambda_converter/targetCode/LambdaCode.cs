using System;
using System.Collections.Generic;
using System.Linq;

namespace lambda_converter.target_code
{
    class LambdaCode
    {
        List<int> ints = new List<int> { 1, 356, 23, 1, 56, 2, 123, 555, 78, 221, 4, 0, 2, 5, 1 };
        int someClassField = 100;
        int anotherClassField = 3;
        int modifiedClassField = 0;

        class UserDefinedType
        {
            public int a;
            float c;
        }

        //Class which is the result of previous conversions applied to this code
        class lambdClass1
        {
            public Int32 methodLambd(Int32 a)
            {
                return a + 1;
            }
        }

        public void Method()
        {
            //SimpleLambdaExpression
            var even = ints.Where(m => m % 2 == 0).ToList();

            int[] externalInts = { 1, 3, 5 };
            int[] localInts = { 3, 6, 9 };

            //ParenthesisedLambdaExpression
            var zipped = localInts.Zip(externalInts, (int m, int n) => { return m - n; }).ToList();

            string text = "result of zipping";
            string teee = "some random text";
            int abba = 123;
            //statement lambda with capture
            zipped.ForEach((n) =>
            {
                Console.WriteLine(text);
                Console.WriteLine(n);
                Console.WriteLine(teee + 3 * abba + text);
            });
            //void argument lambda
            Func<int> voidLam = () => 3;
            //Check if custom value types converted properly
            Func<UserDefinedType, int> custTypeLambda = (UserDefinedType a) => a.a;
            Func<UserDefinedStruct, int> custStructLambda = (UserDefinedStruct b) => b.a - 1;
            //float and double types (value types) converted to  Single, Double struct types
            Func<float, float> floatLambda = (f) => f - 1.3f;
            Func<double, double> doubleLambda = (f) => f - 1.3d;
            Func<Single, Single> float2 = s => 2.0f - s;
            
            //previous conversion
            lambdClass1 lambdInst1 = new lambdClass1();
            Func<Int32, Int32> oldLambd = lambdInst1.methodLambd;
        }

        private class UserDefinedStruct
        {
            public int a;
        }
    }
}
