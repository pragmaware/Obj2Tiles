using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CommandLine;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using Shouldly;

namespace Obj2Tiles.Test;

public class Ktx2ThreadsOptionsTests
{
    [Test]
    public void Ktx2Threads_DefaultsToZero()
    {
        Parse("input.obj", "output").Ktx2Threads.ShouldBe(0);
    }

    [Test]
    public void Ktx2Threads_ParsesPositiveValue()
    {
        var options = Parse("--texture-format", "Ktx2", "--ktx2-threads", "4", "input.obj", "output");

        options.TextureFormat.ShouldBe(TextureFormat.Ktx2);
        options.Ktx2Threads.ShouldBe(4);
    }

    [Test]
    public void Ktx2Threads_NegativeValueIsRejected()
    {
        CheckOptions("--texture-format", "Ktx2", "--ktx2-threads", "-1").ShouldBeFalse();
    }

    [Test]
    public void Ktx2Threads_PositiveValueRequiresKtx2()
    {
        CheckOptions("--ktx2-threads", "4").ShouldBeFalse();
    }

    [Test]
    public void Ktx2Threads_PositiveValueWithKtx2IsAccepted()
    {
        CheckOptions("--texture-format", "Ktx2", "--ktx2-threads", "4").ShouldBeTrue();
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
