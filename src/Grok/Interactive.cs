using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Smith;
using Spectre.Console.Rendering;

namespace Grok;

[Service]
partial class Interactive(IChatClient chat, IHttpClientFactory httpFactory, CancellationTokenSource cts) : IHostedService
{
    static string LatexSize = Env.Get("GROK_LATEX_SIZE", "small");
    static string LatexDpi = Env.Get("GROK_LATEX_DPI", "300");
    static string LatexBg = Env.Get("GROK_LATEX_BG", "white");
    static string LatexFg = Env.Get("GROK_LATEX_FG", "black");

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(InputListener, cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    async Task InputListener()
    {
        AnsiConsole.MarkupLine($":robot: Ready v{ThisAssembly.Info.InformationalVersion}");
        AnsiConsole.Markup($":person_beard: ");

        var history = new List<ChatMessage> { new(ChatRole.System, ThisAssembly.Resources.Prompts.System.Text) };
        var options = new ChatOptions();

        while (true && !cts.IsCancellationRequested)
        {
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input))
                continue;

            if (input.Trim() is "cls" or "clear")
            {
                Console.Clear();
                AnsiConsole.Markup($":person_beard: ");
                continue;
            }

            history.Add(new ChatMessage(ChatRole.User, input));

            try
            {
                var response = await AnsiConsole.Status().StartAsync(":robot: Thinking...",
                    ctx => chat.GetResponseAsync(history, options, cancellationToken: cts.Token));

                history.AddRange(response.Messages);
                // Try rendering as formatted markup
                try
                {
                    if (response.Text is { Length: > 0 })
                    {
                        var markup = new Markup(response.Text);
                        var segments = markup.GetSegments(AnsiConsole.Console);

                        AnsiConsole.Write(new Markup(":robot: "));
                        var line = new List<Segment>();
                        foreach (var segment in segments)
                        {
                            if (LaTeXBlockExpr().Match(segment.Text) is { Success: true } match)
                            {
                                if (match.Groups["prolog"] is { Success: true } prolog && !string.IsNullOrWhiteSpace(prolog.Value))
                                    AnsiConsole.Write(new SegmentsRenderable([new Segment(prolog.Value, segment.Style)]));

                                AnsiConsole.Write(new SegmentsRenderable(line));
                                line.Clear();

                                await RenderLaTeX(match.Groups["latex"].Value);

                                if (match.Groups["epilog"] is { Success: true } epilog && !string.IsNullOrWhiteSpace(epilog.Value))
                                    AnsiConsole.Write(new SegmentsRenderable([new Segment(epilog.Value, segment.Style)]));
                            }
                            else
                            {
                                line.Add(segment);
                            }
                        }

                        if (line.Count > 0)
                            AnsiConsole.Write(new SegmentsRenderable(line));
                    }
                }
                catch (Exception)
                {
                    // Fallback to escaped markup text if rendering fails
                    AnsiConsole.MarkupLineInterpolated($":robot: {response.Text}");
                }

                AnsiConsole.WriteLine();
                AnsiConsole.Markup($":person_beard: ");
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(e);
            }
        }
    }

    async Task RenderLaTeX(string text)
    {
        var query = WebUtility.UrlEncode(@$"\{LatexSize}\dpi{{{LatexDpi}}}\bg{{{LatexBg}}}\fg{{{LatexFg}}}" + text);
        var url = $"https://latex.codecogs.com/png.image?{query}";
        using var client = httpFactory.CreateClient();
        using var response = await client.GetAsync(url);

        if (response.IsSuccessStatusCode)
        {
            using var image = Image.Load<Rgba32>(await response.Content.ReadAsStreamAsync());
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine(image.ToSixel());
            AnsiConsole.WriteLine();
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{response.ReasonPhrase}[/]");
            AnsiConsole.MarkupLineInterpolated($"[grey]{text}[/]");
        }

    }

    [GeneratedRegex(@"(?<prolog>.*?)```latex(?<latex>[\s\S]*?)```(?<epilog>.*?)", RegexOptions.Singleline)]
    private static partial Regex LaTeXBlockExpr();

    class SegmentsRenderable(List<Segment> segments) : IRenderable
    {
        public Measurement Measure(RenderOptions options, int maxWidth)
        {
            var cellCount = Segment.CellCount(segments);
            return new Measurement(cellCount, cellCount);
        }

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth) => segments;
    }
}