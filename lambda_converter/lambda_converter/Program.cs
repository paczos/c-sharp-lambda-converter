using System;
using System.IO;
using static lambda_converter.LambdaConverter;

namespace lambda_converter
{
    class Program
    {
        const string CODELOCATION = @".\targetCode\LambdaCode.cs";
        const string RESULTLOCATION = @".\targetCode\NonLambdaCode.cs";

        static void Main(string[] args)
        {
            try
            {
                var code = LambdaConverter.Convert(File.ReadAllText(CODELOCATION));
                var lines = code.Split('\n');
                using (StreamWriter sr = new StreamWriter(RESULTLOCATION))
                {
                    foreach (var line in lines)
                    {
                        sr.WriteLine(line);
                    }
                }

            }
            catch (IOException ex)
            {
                Console.Error.WriteLine("IO Exception-cannot write output to file" + ex.ToString());

            }
            catch (NotSupportedException ex)
            {
                Console.Error.WriteLine("Some tranformations on code are not supported" + ex.ToString());

            }
            catch (UnsupportedCodeTransformationException ex)
            {
                Console.Error.WriteLine(ex.ToString());
            }
        }
    }
}