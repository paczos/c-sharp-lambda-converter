using System;
using System.IO;
using static lambda_converter.LambdaConverter;

namespace lambda_converter
{
    class Program
    {
        const string CODELOCATION = @".\targetCode\LambdaCode.cs";

        static void LoadConvertWrite(string path = CODELOCATION)
        {
            var code = LambdaConverter.Convert(File.ReadAllText(path));
            File.WriteAllText(CODELOCATION + ".converted", code);
        }
        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    LoadConvertWrite();
                }
                else
                {
                    foreach (var path in args)
                    {
                        LoadConvertWrite(path);
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