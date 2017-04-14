using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lambda_converter.target_code
{
    class LambdaCode
    {
        List<int> ints = new List<int> { 1, 356, 23, 1, 56, 2, 123, 555, 78, 221, 4, 0, 2, 5, 1 };

        public delegate int fun(int a, float b);
        int m(int c, float b) { return c; }

        public void Method()
        {
            fun el = new fun(m);
            var res = el(3, 2.4f);
            var even = ints.Where((m) => m % 2 == 0).ToList();

            int[] externalInts = { 1, 3, 5 };
            int[] localInts = { 3, 6, 9 };

            //expression lambda
            var zipped = localInts.Zip(externalInts, (int m, int n) => { return m - n; }).ToList();

            string text = "result of zipping";

            //statement lamda with capture
            zipped.ForEach(n =>
            {

                Console.WriteLine(text);
                Console.WriteLine(n);
            });

            Func<float, float> lam = (float o) => o - 1.3f;
            lam(5.0f);

            //Func<int> voidLam = () => 3;


            //nested lambda
            // Func<int,Func<int>> nested = (b) => () => b*3;

            //recursive lambda
            //Func<Func<int, int>, Func<int, int>> factorial = (fac) => x => x == 0 ? 0 : x * fac(x - 1); 


        }

    }
}
