using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CommandLine;
using NUnit.Framework;
using Shouldly;

namespace Obj2Tiles.Test;

public class Ktx2ZstdOptionsTests
{
    [Test]
    public void Ktx2ZstdLevel_DefaultsToDisabled()
    {
        Parse("input.obj", "output").Ktx2ZstdLevel.ShouldBe(0);
        CheckOptions().ShouldBeTrue();
    }

    [Test]
    public void Ktx2ZstdLevel_ParsesValidLevel()
    {
        var options = Parse("--texture-format", "Ktx2", "--ktx2-uastc", "--ktx2-zstd-level", "18", "input.obj", "output");

        options.Ktx2Uastc.ShouldBeTrue();
        options.Ktx2ZstdLevel.ShouldBe(18);
    }

    [TestCase(-1)]
    [TestCase(23)]
    public void Ktx2ZstdLevel_OutOfRangeIsRejected(int level)
    {
        CheckOptions("--texture-format", "Ktx2", "--ktx2-uastc", "--ktx2-zstd-level", level.ToString()).ShouldBeFalse();
    }

    [Test]
    public void Ktx2ZstdLevel_RequiresKtx2()
    {
        CheckOptions("--ktx2-uastc", "--ktx2-zstd-level", "18").ShouldBeFalse();
    }

    [Test]
    public void Ktx2ZstdLevel_RequiresUastc()
    {
        CheckOptions("--texture-format", "Ktx2", "--ktx2-zstd-level", "18").ShouldBeFalse();
    }

    [Test]
    public void Ktx2ZstdLevel_UastcKtx2IsAccepted()
    {
        CheckOptions("--texture-format", "Ktx2", "--ktx2-uastc", "--ktx2-zstd-level", "18").ShouldBeTrue();
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

    private static bool CheckOptions(params string[] optionArgs)
    {
        var input = Path.GetTempFileName();
        try
        {
            var args = optionArgs.Concat([input, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())]).ToArray();
            var options = Parse(args);
            var programType = typeof(Options).Assembly.GetType("Obj2Tiles.Program", throwOnError: true)!;
            var method = programType.GetMethod("CheckOptions", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (bool)method.Invoke(null, [options])!;
        }
        finally
        {
            File.Delete(input);
        }
    }
}
