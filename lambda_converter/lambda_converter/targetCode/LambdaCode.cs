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
            //statement lamda with capture
            zipped.ForEach((n) =>
            {
                Console.WriteLine(text);
                Console.WriteLine(n);
                Console.WriteLine(teee + 3 * abba + text);
            });



            int someClassField = 0;
            //this will result in error (referencing class field):
            Func<int, int> sideEffects = (n) =>
            {
                Console.WriteLine(text);
                Console.WriteLine(n + this.someClassField);
                someClassField++;
                return n % 2;
            };
            //this time class field  anotherClassField is not hidden
            sideEffects = (n) =>
            {
                Console.WriteLine(text);
                Console.WriteLine(n + anotherClassField);
                modifiedClassField = 1;
                return n % 2;
            };

            //but after small modification it can work:
            LambdaCode lam = this;
            sideEffects = (n) =>
            {
                Console.WriteLine(text);
                Console.WriteLine(n+lam.someClassField);
                return n % 2;
            };            





            //Func<int> voidLam = () => 3;
            
            //nested lambda-this should be converted partially
            //Func<int,Func<int>> nested = (b) => () => b*3;

            //recursive lambda - this should be converted partially
           // Func<Func<int, int>, Func<int, int>> factorial = (fac) => x => x == 0 ? 0 : x * fac(x - 1); 
        }
    }
}
