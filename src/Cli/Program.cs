using System.Runtime.InteropServices;

using System.Text.Json;

var info = new
{
    OS = RuntimeInformation.OSDescription,
    Arch = RuntimeInformation.ProcessArchitecture.ToString(),
    Runtime = RuntimeInformation.FrameworkDescription,
    BaseDir = AppContext.BaseDirectory,
    CurrentDir = Environment.CurrentDirectory
};

if (args.Contains("--json"))
{
    Console.WriteLine(JsonSerializer.Serialize(info));
}
else
{
    Console.WriteLine("CrossApp - Course project, Cross-Platform Programming");
    Console.WriteLine("Student: Shvorob Maksym, FEI - 37");
    Console.WriteLine(new string('*', 52));
    Console.WriteLine($"OS (Description) : {RuntimeInformation.OSDescription}");
    Console.WriteLine($"OS (Environment) : {Environment.OSVersion}");
    Console.WriteLine($"Architecture : {RuntimeInformation.ProcessArchitecture}");
    Console.WriteLine($".NET Version (CLR) : {Environment.Version}");
    Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
    Console.WriteLine($"App path : {AppContext.BaseDirectory}");
    Console.WriteLine($"Current path : {Environment.CurrentDirectory}");
    Console.WriteLine(new string('*', 52));
    Console.WriteLine("Subject Area: Order (Customer, Product, Category, Orderm OrderLine)");
}

