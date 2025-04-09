using System.Net;
using System.Text.Json;
using Newtonsoft.Json;

var client = new HttpClient();
var apiKey = "";

if (File.Exists("key.txt"))
{
    var fileKey = File.ReadAllText("key.txt");
    if (!string.IsNullOrWhiteSpace(fileKey) && await ApiKeyIsValidAsync(fileKey))
        apiKey = fileKey;
}

while (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Write("Enter API key: ");
    var inputKey = Console.ReadLine();

    if (!string.IsNullOrWhiteSpace(inputKey) && await ApiKeyIsValidAsync(inputKey))
        apiKey = inputKey;
    else
        Console.WriteLine("Please enter a valid API key.\n");
}

while (true)
{
    Console.WriteLine("AiCli - Options");
    Console.WriteLine("1. Text prompt (formatted)");
    Console.WriteLine("2. Text prompt (unformatted)");
    Console.WriteLine("3. File prompt");
    Console.WriteLine("4. Close/quit\n");
    Console.Write("Select an option: ");
    var option = Console.ReadLine();

    switch (option)
    {
        case "1":
        {
            Console.Write("Enter a prompt: ");
            var prompt = Console.ReadLine();

            if (string.IsNullOrEmpty(prompt))
            {
                Console.WriteLine("Invalid entry!");
                continue;
            }
        
            await AskAiAsync(prompt);
            break;
        }
        case "2":
        {
            Console.Write("Enter a prompt: ");
            var prompt = Console.ReadLine();

            if (string.IsNullOrEmpty(prompt))
            {
                Console.WriteLine("Invalid entry!");
                continue;
            }
        
            await AskAiAsync(prompt, false);
            break;
        }
        case "3":
        {
            Console.Write("Enter an absolute file path: ");
            var filePath = Console.ReadLine();
        
            if (string.IsNullOrEmpty(filePath))
            {
                Console.WriteLine("Invalid entry!");
                continue;
            }

            var exists = File.Exists(filePath);
            if (!exists)
            {
                Console.WriteLine("File does not exist!");
                continue;
            }
        
            var fileContent = File.ReadAllText(filePath);
            await AskAiAsync(fileContent);
            break;
        }
        case "4" or "q":
            Environment.Exit(0);
            break;
    }
}

async Task<bool> ApiKeyIsValidAsync(string apiKey)
{
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    
    var response = await client.GetAsync("https://api.openai.com/v1/models");

    return response.IsSuccessStatusCode;
}

async Task AskAiAsync(string prompt, bool usePrePrompt = true)
{
    var timer = new System.Diagnostics.Stopwatch();
    timer.Start();

    var prePrompt = "The content you output will be displayed in a terminal, not in a beautiful UI. Please keep " +
                    "this in mind as markdown, emojis, codeblocks, etc will not display the way you think they will. " +
                    "Your prompt is below:\n\n";
    
    var json = new
    {
        model = "gpt-4o",
        input = usePrePrompt ? prePrompt + prompt : prompt,
        stream = true
    };
    
    var httpContent = new StringContent(JsonConvert.SerializeObject(json), System.Text.Encoding.UTF8, "application/json");
    var response = await client.PostAsync("https://api.openai.com/v1/responses", httpContent);
    
    if (response.StatusCode == HttpStatusCode.OK)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        
        Console.WriteLine();
        
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line) || !line.TrimStart().StartsWith("data:")) continue;
            
            var jsonLine = line.Substring("data:".Length).Trim();
            if (jsonLine == "[DONE]") break;

            var jsonDoc = JsonDocument.Parse(jsonLine);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("type", out var typeElement)
                || typeElement.GetString() != "response.output_text.delta") continue;
            
            var delta = root.GetProperty("delta");
            Console.Write(delta.GetString());
            Console.Out.Flush();
        }
        
        timer.Stop();
        
        Console.WriteLine("\n--------------------------------------");
        var seconds = timer.ElapsedMilliseconds / 1000;
        var millis = timer.ElapsedMilliseconds % 1000;        Console.WriteLine($"Response generated in {seconds}.{millis:D3}s\n");
    }
}