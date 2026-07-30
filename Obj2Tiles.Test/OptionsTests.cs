using CommandLine;
using NUnit.Framework;
using Shouldly;

namespace Obj2Tiles.Test;

public class OptionsTests
{
    [Test]
    public void NoRootContent_DefaultsToFalse()
    {
        Parse("input.obj", "output").NoRootContent.ShouldBeFalse();
    }

    [Test]
    public void NoRootContent_ParsesFlag()
    {
        Parse("--no-root-content", "input.obj", "output").NoRootContent.ShouldBeTrue();
    }

    private static Options Parse(params string[] args)
    {
        Options? options = null;
        using var parser = new Parser(settings => settings.HelpWriter = null);
        var result = parser.ParseArguments<Options>(args);
        result.WithParsed(parsed => options = parsed);
        result.Tag.ShouldBe(ParserResultType.Parsed);
        return options!;
    }
}
