namespace LazyTuiHub.Cli;

public static class ConsoleArguments
{
    public static string[] Parse(string[] arguments)
    {
        var options = new List<string>();
        var position = 0;
        var index = 0;
        while (index < arguments.Length)
        {
            var value = arguments[index++];
            if (value is "--help" or "-h")
                options.Add("--cli-help=true");
            else if (value.StartsWith('-') || value.Contains('='))
            {
                options.Add(value);
                if (!value.Contains('=') && index < arguments.Length)
                    options.Add(arguments[index++]);
            }
            else
            {
                var key = position++ switch
                {
                    0 => "command",
                    1 => "action",
                    _ => throw new ArgumentException(
                        $"Unexpected argument '{value}'. Use GROUP ACTION followed by named options.",
                        nameof(arguments)
                    ),
                };
                options.Add($"--{key}={value}");
            }
        }
        return options.ToArray();
    }
}
