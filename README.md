# Project description #

A21 Program that converts code with lambda expressions into code without lambda expressions. The code is a subset of Java or C#.

* [x] TODO: brief introduction to lambda calculus
* [x] TODO: describe roslyn and its data structures
* [x] TODO: transformation principles as a list of tasks



## Idea ##

As input the program receives path to a file with source code written in C# language that may contain lambda expressions of various kind. The program performs transformations of several kind on the code in order to output modified version of the code that contains no lambda expressions.

Accepted types of lambda expressions are:

* simple lambda expressions
eg. 
```
#!C#

Func<int, int> lambd1 = m => m -1;
```

* parenthesized lambda expressions

```
#!C#

Func<int, int, int> lambd2 = (int m, int n) => (m - n);
```

* lambda expressions containing statements  (so called statement lambdas), side effects

```
#!C#

Func<int, int> lambd3 = (n) =>
{
    Console.WriteLine(text);
    Console.WriteLine(n);
    return n % 2;
};

```
* lambda expressions capturing local variables

```
#!C#
string text = "this is some local text";
Func<string, string> lambd4 = (n) => (text+n);
```

## Architecture and transformation principles ##
Conversion is a process that is split into several stages.

* syntax analyzer
* semantic analyzer
* transformer

In the first stage code is passed to syntax analyzer that is used to find portions of the code that are lambda expressions. 

The next stage makes use of semantic analyzer which infers types of arguments passed to the lambda expressions and looks for variables that are captured in the body.

After this, using transformer, a nested private class containing single method with signature same as lambda expression is constructed. If the lambda expression captures local variables, newly created class has fields that correspond to them. In the place where lambda expression is used, an instance of the new class is defined and its fields are populated with local variables. Next, instance's method is passed in place where lambda expression used to be.


# Example of input and output #

```
#!C#

using System;
using System.Collections.Generic;
using System.Linq;

namespace lambda_converter.target_code
{
    class LambdaCode
    {

        List<int> ints = new List<int> { 1, 356, 23, 1, 56, 2, 123, 555, 78, 221, 4, 0, 2, 5, 1 };

        public void Method()
        {
            var res = el(3, 2.4f);
            var even = ints.Where(m => m % 2 == 0).ToList();

            int[] externalInts = { 1, 3, 5 };
            int[] localInts = { 3, 6, 9 };

            //expression lambda
            var zipped = localInts.Zip(externalInts, (int m, int n) => { return m - n; }).ToList();

            string text = "result of zipping";

            //statement lamda with capture
            zipped.ForEach((n) =>
            {
                Console.WriteLine(text);
                Console.WriteLine(n);
            });

            Func<float, float> lam = (float o) => o - 1.3f;
            lam(5.0f);

            //Func<int> voidLam = () => 3;

            //nested lambda
            //Func<int,Func<int>> nested = (b) => () => b*3;
        }
    }
}
```


```
#!C#

using System;
using System.Collections.Generic;
using System.Linq;

namespace lambda_converter.target_code
{
    class LambdaCode
    {
        class lam1
        {
            bool method1 (int m)
            {
                return m % 2==0;
            }
        }
        class lam2
        {
            int method1 (int m, int n)
            {
                return m - n;
            }
        }

        class lam3
        {
            public string text;
            void method1 (int n)
            {
                Console.WriteLine(text);
                Console.WriteLine(n);
            }
        }
        class lam4
        {
            float method1 (float o)
            {
                return  o - 1.3f;
            }
        }
        List<int> ints = new List<int> { 1, 356, 23, 1, 56, 2, 123, 555, 78, 221, 4, 0, 2, 5, 1 };

        public void Method()
        {
            var res = el(3, 2.4f);
            lam1 inst1 = new lam1();
            var even = ints.Where(inst1.method1).ToList();

            int[] externalInts = { 1, 3, 5 };
            int[] localInts = { 3, 6, 9 };

            //expression lambda
            lam2 inst2 = new lam2();
            var zipped = localInts.Zip(externalInts, inst2.method1).ToList();

            string text = "result of zipping";

            //statement lamda with capture
            lam3 inst3 = new lam3();
            inst3.text = text;
            zipped.ForEach(inst3.method1);

            lam4 inst4 = new lam4();
            Func<float, float> lam = inst4.method1;
            lam(5.0f);

            //Func<int> voidLam = () => 3;

            //nested lambda
            //Func<int,Func<int>> nested = (b) => () => b*3;

        }
    }
}
```