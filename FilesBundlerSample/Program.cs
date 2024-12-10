using System.CommandLine;
using fib;

var bundleCommand = new Command("bundle", "Bundle code files to a single file");

// Define options with short aliases
var languageOption = new Option<string>("--language", "List of programming languages (use 'all' for all files)") { IsRequired = true };
languageOption.AddAlias("-l");
var outputOption = new Option<FileInfo>("--output", "Output bundle file name (can be a full path)");
outputOption.AddAlias("-o");
var noteOption = new Option<bool>("--note", "Include source code as a comment in the bundle");
noteOption.AddAlias("-n");
var sortOption = new Option<string>("--sort", "Sort files by name or type (default is by name)");
sortOption.AddAlias("-s");
var removeEmptyLinesOption = new Option<bool>("--remove-empty-lines", "Remove empty lines from the code");
removeEmptyLinesOption.AddAlias("-r");
var authorOption = new Option<string>("--author", "Name of the file creator");
authorOption.AddAlias("-a");

bundleCommand.AddOption(languageOption);
bundleCommand.AddOption(outputOption);
bundleCommand.AddOption(noteOption);
bundleCommand.AddOption(sortOption);
bundleCommand.AddOption(removeEmptyLinesOption);
bundleCommand.AddOption(authorOption);

bundleCommand.SetHandler((language, output, note, sort, removeEmptyLines, author) =>
{
    // Input Validation
    if (string.IsNullOrWhiteSpace(language))
    {
        Console.WriteLine("Error: Language option is required.");
        return;
    }

    // Convert language string to enum
    var languages = language.Split(',')
                            .Select(lang => lang.Trim())
                            .ToList();

    var validLanguages = new List<Fib.ProgrammingLanguages>();
    bool allLanguagesSelected = false;

    foreach (var lang in languages)
    {
        if (lang.ToLower() == "all")
        {
            allLanguagesSelected = true; // Set a flag to indicate 'all' is selected
            break; // Exit the loop, as we will consider all files
        }

        Fib.ProgrammingLanguages selectedLanguage;
        if (Enum.TryParse(lang, true, out selectedLanguage))
        {
            validLanguages.Add(selectedLanguage);
        }
        else
        {
            Console.WriteLine($"Error: Invalid language option '{lang}'.");
        }
    }

    // Proceed only if there are valid languages or if 'all' is selected
    if (!allLanguagesSelected && validLanguages.Count == 0)
    {
        Console.WriteLine("No valid languages were found. Please specify at least one valid language.");
        return; // Exit if no valid languages were found
    }


    // Exclude specific directories
    var excludedDirectories = new[] { "bin", "debug", "Debug", "Bin", "obj" };
    var filesToBundle = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.*", SearchOption.AllDirectories)
                                  .Where(file => !excludedDirectories.Any(dir =>
                                      Path.GetFullPath(file).IndexOf(Path.Combine(Directory.GetCurrentDirectory(), dir), StringComparison.OrdinalIgnoreCase) >= 0))
                                  .ToList();

    // Filter files based on valid languages
    if (allLanguagesSelected)
    {
        filesToBundle = filesToBundle.Where(file =>
        {
            var fileExtension = Path.GetExtension(file).ToLower();
            try
            {
                var fileLanguage = GetProgrammingLanguageByFileExtension(fileExtension);
                return Enum.IsDefined(typeof(Fib.ProgrammingLanguages), fileLanguage); // Check if the language is defined in the enum
            }
            catch (ArgumentException)
            {
                return false; // Ignore files with unknown extensions
            }
        }).ToList();
    }
    else
    {
        filesToBundle = filesToBundle.Where(file =>
        {
            var fileExtension = Path.GetExtension(file).ToLower();
            if (string.IsNullOrEmpty(fileExtension))
            {
                Console.WriteLine($"Warning: File '{file}' has no extension.");
                return false; // Return false if there is no extension
            }

            Fib.ProgrammingLanguages fileLanguage;
            try
            {
                fileLanguage = GetProgrammingLanguageByFileExtension(fileExtension);
            }
            catch (ArgumentException)
            {
                return false; // Return false if there is an error in determining the language
            }
            return validLanguages.Contains(fileLanguage); // Return result
        }).ToList();
    }

    // Sorting logic based on the sort option
    if (!string.IsNullOrWhiteSpace(sort))
    {
        if (sort.ToLower() == "type")
        {
            filesToBundle = filesToBundle.OrderBy(file => Path.GetExtension(file)).ThenBy(file => file).ToList();
        }
        else if (sort.ToLower() == "name")
        {
            filesToBundle = filesToBundle.OrderBy(file => Path.GetFileName(file)).ToList();
        }
        else
        {
            throw new ArgumentException("Invalid sort option. Please use 'type' or 'name'.");
        }
    }
    else
    {
        filesToBundle = filesToBundle.OrderBy(file => Path.GetFileName(file)).ToList();
    }

    // Validate output path
    if (output == null)
    {
        Console.WriteLine("Error: output option is required.");
        return;
    }
    else if (!Directory.Exists(Path.GetDirectoryName(output.FullName)))
    {
        Console.WriteLine("Error: Output file path is invalid.");
        return;
    }
    if (File.Exists(output.FullName))
    {
        Console.WriteLine("Warning: Output file already exists. Do you want to overwrite it? (yes/no)");
        string response = Console.ReadLine();

        if (response?.ToLower() == "no")
        {
            return;
        }
    }

    // Process the input and bundle files based on the options provided.
    try
    {
        using (var stream = File.Create(output.FullName))
        {
            using (var writer = new StreamWriter(stream))
            {
                if (author != null)
                {
                    writer.WriteLine($"// Bundled by {author}");
                }

                foreach (var file in filesToBundle)
                {
                    var sourceFileName = Path.GetFileName(file);
                    var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), file);

                    if (note)
                    {
                        writer.WriteLine($"// Source Code: {sourceFileName}, Path: {relativePath}");
                    }

                    var content = File.ReadAllText(file);
                    if (removeEmptyLines)
                    {
                        content = string.Join("\n", content.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)));
                    }
                    writer.WriteLine(content);
                }
            }
        }
        Console.WriteLine("Files were bundled successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}, languageOption, outputOption, noteOption, sortOption, removeEmptyLinesOption, authorOption);


Fib.ProgrammingLanguages GetProgrammingLanguageByFileExtension(string fileExtension)
{
    return fileExtension switch
    {
        ".cs" => Fib.ProgrammingLanguages.CSharp,
        ".java" => Fib.ProgrammingLanguages.Java,
        ".py" => Fib.ProgrammingLanguages.Python,
        ".ipynb" => Fib.ProgrammingLanguages.Python,
        ".js" => Fib.ProgrammingLanguages.JavaScript,
        ".rb" => Fib.ProgrammingLanguages.Ruby,
        ".php" => Fib.ProgrammingLanguages.PHP,
        ".go" => Fib.ProgrammingLanguages.Go,
        ".swift" => Fib.ProgrammingLanguages.Swift,
        ".rs" => Fib.ProgrammingLanguages.Rust,
        ".cpp" => Fib.ProgrammingLanguages.CPlusPlus,
        ".c" => Fib.ProgrammingLanguages.C,
        ".dart" => Fib.ProgrammingLanguages.Dart,
        // Add other mappings as necessary
        _ => throw new ArgumentException($"Unknown file extension: {fileExtension}")
    };
}



// Create the create-rsp command
var createRspCommand = new Command("create-rsp", "Create a response file for the bundle command");

createRspCommand.SetHandler(async () =>
{
    Console.Write("Enter languages (or 'all'): ");
    var language = Console.ReadLine();

    Console.Write("Enter output file name: ");
    var output = Console.ReadLine();

    Console.Write("Include note? (true/false): ");
    var note = bool.Parse(Console.ReadLine());

    Console.Write("Sort by (name/type): ");
    var sort = Console.ReadLine();

    Console.Write("Remove empty lines? (true/false): ");
    var removeEmptyLines = bool.Parse(Console.ReadLine());

    Console.Write("Enter author name: ");
    var author = Console.ReadLine();

    var responseFileName = "response.rsp";
    using (var writer = new StreamWriter(responseFileName))
    {
        writer.WriteLine($"--language {language} --output {output} --note {note} --sort {sort} --remove-empty-lines {removeEmptyLines} --author \"{author}\"");
    }
    Console.WriteLine($"Response file created: {responseFileName}");
});

var rootCommand = new RootCommand("Root command for File Bundler CLI");
rootCommand.AddCommand(bundleCommand);
rootCommand.AddCommand(createRspCommand);

await rootCommand.InvokeAsync(args);
