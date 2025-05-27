using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace CSharpCodeFixer
{
    public static class CodeFixerExtension
    {
        /// <summary>
        /// Returns all files in the given root directory with specified extensions,
        /// excluding files inside specific folders (e.g., bin, obj).
        /// </summary>
        /// <param name="projectDirectory">Root directory to start the search</param>
        /// <param name="fileExtensions">List of file extensions to include (e.g., ".cs", ".xaml")</param>
        /// <param name="excludedFolders">List of folder names to exclude (e.g., "bin", "obj")</param>
        /// <returns>List of full paths to matching files</returns>
        public static List<string> GetCodeFiles(
            this string projectDirectory,
            List<string> fileExtensions,
            List<string> excludedFolders = null)
        {
            if (excludedFolders == null)
            {
                excludedFolders = new List<string> { "bin", "obj" };
            }

            if (fileExtensions == null || fileExtensions.Count == 0)
            {
                throw new ArgumentException("You must provide at least one file extension.");
            }

            var matchingFiles = new List<string>();

            try
            {
                var directories = Directory.GetDirectories(projectDirectory, "*", SearchOption.AllDirectories)
                    .Where(dir => !excludedFolders.Any(excluded =>
                        dir.Split(Path.DirectorySeparatorChar).Contains(excluded, StringComparer.OrdinalIgnoreCase)));

                foreach (var dir in directories.Prepend(projectDirectory)) // Include the root path itself
                {
                    foreach (var ext in fileExtensions)
                    {
                        matchingFiles.AddRange(Directory.GetFiles(dir, $"*{ext}", SearchOption.TopDirectoryOnly));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while scanning files: {ex.Message}");
            }

            return matchingFiles;
        }

        /// <summary>
        /// Asynchronously analyzes and corrects C# files using a local AI model via the Ollama API.
        /// </summary>
        /// <param name="files">List of C# file paths to analyze and correct.</param>
        /// <param name="OutputDirectory">Directory where the corrected files will be saved.</param>
        /// <param name="OllamaModel">The name of the Ollama model to use (e.g., "codellama").</param>
        /// <param name="OllamaApiUrl">The URL of the local Ollama API endpoint (default: http://localhost:11434/api/generate).</param>
        /// <param name="MaxChars">Maximum number of characters to include in the AI prompt per file (default: 10,000).</param>
        /// <param name="timeoutInMinutes">Timeout in minutes for the HTTP request (default: 30).</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains a list of <see cref="CorrectedFile"/> objects with the file paths and analysis results.
        /// </returns>
        public static async Task<List<CorrectedFile>> GetCorrectedCodeFiles(
            this List<string> files,
            string OutputDirectory,
            string OllamaModel,
            string OllamaApiUrl = "http://localhost:11434/api/generate",
            int MaxChars = 10000,
            int timeoutInMinutes = 30)
        {
            var http = new HttpClient() { Timeout = TimeSpan.FromMinutes(timeoutInMinutes) };

            List<CorrectedFile> correctedfiles = new List<CorrectedFile>();

            foreach (var file in files)
            {
                try
                {
                    string code = await File.ReadAllTextAsync(file);
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        Console.WriteLine($"Skipping empty file: {Path.GetFileName(file)}");
                        continue;
                    }

                    code = code.Length > MaxChars ? code.Substring(0, Math.Min(MaxChars, code.Length)) : code;

                    var fileName = Path.GetFileName(file);

                    var prompt = $@"
                            You are a C# static analyzer. Your task is to analyze the provided C# code from the file '{fileName}' and perform the following:

                            - For errors, provide a corrected version of the entire code.
                            Or,
                            - For success, do not provide any code.

                            Here is the original code to analyze:

                            ```csharp
                            {code}
                            ```
                            ";

                    var request = new
                    {
                        model = OllamaModel,
                        prompt,
                        stream = false
                    };

                    Console.WriteLine($"Analyzing {fileName}");

                    var response = await http.PostAsJsonAsync(OllamaApiUrl, request).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode(); // Throws if the response is not successful

                    var result = await response.Content.ReadFromJsonAsync<OllamaResponse>().ConfigureAwait(false);

                    if (result?.Response != null)
                    {
                        string outputFilePath = Path.Combine(OutputDirectory, fileName);
                        correctedfiles.Add(new CorrectedFile { FilePath = outputFilePath, AnalyzedResult = result.Response.Trim() });

                        string pattern = @"```csharp\s*(.*?)```"; // non-greedy match between code block
                        Match match = Regex.Match(result.Response.Trim(), pattern, RegexOptions.Singleline);

                        if (match.Success)
                        {
                            string extractedCode = match.Groups[1].Value;
                            Console.WriteLine("Corrected Code:\n" + extractedCode);

                            await File.WriteAllTextAsync(outputFilePath, extractedCode);
                        }
                        else
                        {
                            Console.WriteLine("No corrected code found.");
                        }

                    }
                    else
                    {
                        //log
                        //report.Add($"File: {file}\nNo analysis returned from the API.\n{new string('-', 80)}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error analyzing {Path.GetFileName(file)}: {ex.Message}");
                    //log
                    //report.Add($"File: {file}\nError during analysis: {ex.Message}\n{new string('-', 80)}");
                }
            }

            return correctedfiles;
        }

        public static async Task CorrectOriginalCodeFiles(
        this List<string> originalCodeFiles,
        List<CorrectedFile> correctedCodeFiles)
        {
            try
            {
                foreach (var correctedCodeFile in correctedCodeFiles)
                {
                    // Find the original file path that matches the corrected file (case-sensitive)
                    var matchingOriginalFile = originalCodeFiles
                        .FirstOrDefault(orig =>
                            string.Equals(Path.GetFileName(orig), Path.GetFileName(correctedCodeFile.FilePath), StringComparison.Ordinal));

                    if (matchingOriginalFile != null && File.Exists(matchingOriginalFile))
                    {
                        await File.WriteAllTextAsync(matchingOriginalFile, await File.ReadAllTextAsync(correctedCodeFile.FilePath));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while correcting code files: " + ex.Message);
                // You might want to log or handle the exception more robustly depending on the context.
            }
        }

    }

}
