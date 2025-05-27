using CSharpCodeFixer;

string codebaseDirectory =  @"D:\My Study\AI\GitHub ahmad-act\New folder (2)\CodesToAITesting";
string outputDirectory = "D:\\My Study\\AI\\GitHub ahmad-act\\New folder (2)\\output";
var fileExtensions = new List<string> { ".cs" };
var excludedFolder = new List<string> { "bin", "obj" };

List<string> originalCodeFiles = codebaseDirectory.GetCodeFiles(fileExtensions, excludedFolder);

var correctedCodeFiles = await originalCodeFiles.GetCorrectedCodeFiles(outputDirectory, "codellama");

await originalCodeFiles.CorrectOriginalCodeFiles(correctedCodeFiles);

foreach (var correctedCodeFile in correctedCodeFiles)
{
    Console.WriteLine("FilePath: ", correctedCodeFile.FilePath);
    Console.WriteLine("AnalyzedResult: ", correctedCodeFile.AnalyzedResult);
}