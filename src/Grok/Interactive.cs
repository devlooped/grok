namespace Grok;

[Service]
partial class Interactive(IChatClient chat, CancellationTokenSource cts) : IHostedService
{
    public const string SystemFormatting =
        """
        Your responses will be rendered using Spectre.Console.AnsiConsole.Write(new Markup(string text))). 
        This means that you can use rich text formatting, colors, and styles in your responses, but you must 
        ensure that the text is valid markup syntax. 
        """;


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

        var history = new List<ChatMessage> { new ChatMessage(ChatRole.System, SystemFormatting) };

        while (true && !cts.IsCancellationRequested)
        {
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input))
                continue;

            history.Add(new ChatMessage(ChatRole.User, input));

            try
            {
                var response = await AnsiConsole.Status().StartAsync(":robot: Thinking...",
                    ctx => chat.GetResponseAsync(history, cancellationToken: cts.Token));

                history.AddRange(response.Messages);
                // Try rendering as formatted markup
                try
                {
                    if (response.Text is { Length: > 0 })
                        AnsiConsole.MarkupLine($":robot: {response.Text}");
                }
                catch (Exception)
                {
                    // Fallback to escaped markup text if rendering fails
                    AnsiConsole.MarkupLineInterpolated($":robot: {response.Text}");
                }

                AnsiConsole.Markup($":person_beard: ");
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(e);
            }
        }
    }
}