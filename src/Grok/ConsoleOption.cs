using Mono.Options;

namespace Grok;

class ConsoleOption : OptionSet
{
    public ConsoleOption() => Add("k|key", "Grok API key", k => ApiKey = k);

    public string? ApiKey { get; private set; }
}
