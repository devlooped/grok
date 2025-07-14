using System.Diagnostics;
using System.Text.RegularExpressions;
using Devlooped.Extensions.AI.Grok;
using Grok;
using Microsoft.Extensions.Logging;
using Smith;

var debug = false;
var help = false;
var version = false;

var options = new ConsoleOption
{
    { "?|h|help", "Display this help.", h => help = h != null },
    { "d|debug", "Debug the WhatsApp CLI.", d => debug = d != null, true },
    { "v|version", "Render tool version and updates.", v => version = v != null },
};

options.Parse(args);

if (debug)
    Debugger.Launch();

if (help)
{
    AnsiConsole.MarkupLine("Usage: [green]grok[/] [grey][[OPTIONS]]+[/]");
    AnsiConsole.WriteLine("Options:");
    options.WriteOptionDescriptions(Console.Out);
    return 0;
}

var apiKey = options.ApiKey ?? Env.Get("XAI_API_KEY");

if (!string.IsNullOrEmpty(options.ApiKey))
{
    var envFile = SaveApiKey(options.ApiKey);
    DotNetEnv.Env.Load(envFile);
}
else if (string.IsNullOrEmpty(apiKey))
{
    apiKey = AnsiConsole.Ask<string>("Enter Grok API Key: ");
    if (string.IsNullOrEmpty(apiKey))
    {
        AnsiConsole.MarkupLine("[red]API key cannot be empty.[/]");
        return 1;
    }

    var envFile = SaveApiKey(apiKey);
    DotNetEnv.Env.Load(envFile);
}

var host = Host.CreateApplicationBuilder(args);
host.Logging.ClearProviders();
host.Services.AddServices();
host.Services.AddHttpClient();
host.Services.AddChatClient(new GrokChatClient(Env.Get("XAI_API_KEY")!, "grok-4", new OpenAI.OpenAIClientOptions
{
    NetworkTimeout = TimeSpan.FromMinutes(15),
}));

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => cts.Cancel();
host.Services.AddSingleton(cts);

var app = host.Build();

if (version)
{
    app.ShowVersion();
    await app.ShowUpdatesAsync();
    return 0;
}

#if DEBUG
await app.RunWithUpdatesAsync(cts.Token);
#else
await app.RunAsync(cts.Token);
#endif

return 0;

static string SaveApiKey(string apiKey)
{
    // We always save to the user's profile .env
    var userEnv = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".env");
    if (File.Exists(userEnv))
    {
        if (Regex.IsMatch(File.ReadAllText(userEnv), @"XAI_API_KEY=(?<key>[^\n|\s|\b|\$]+)"))
        {
            // replace captured group with the new API key, save to file
            var content = File.ReadAllText(userEnv);
            File.WriteAllText(userEnv, Regex.Replace(content, @"XAI_API_KEY=(?<key>[^\n|\s|\b|\$]+)", "XAI_API_KEY=" + apiKey));
        }
        else
        {
            // append the new API key to the file
            File.AppendAllText(userEnv, Environment.NewLine + "XAI_API_KEY=" + apiKey);
        }
    }
    else
    {
        File.WriteAllText(userEnv, "XAI_API_KEY=" + apiKey);
    }

    return userEnv;
}