// See https://aka.ms/new-console-template for more information
using CSharpPythonWrapper;

var wrapper = new PythonWrapper();
Console.WriteLine("About to call python script");
var result = wrapper.Execute("/workspaces/ai-playground/src/python/deeplearningai/simplechain.py");
Console.WriteLine("Result: " + result);
Console.WriteLine("Press a key to continue");
Console.ReadLine();
