using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class UiTextTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Counted_KeepsBothFormsAndPassesTheCountAsTheFirstArgument(int count)
    {
        var text = UiText.Counted("Trained {0} generation for {1}", "Trained {0} generations for {1}", count, "Walker");

        text.Message.ShouldBe("Trained {0} generation for {1}");
        text.Plural.ShouldBe("Trained {0} generations for {1}");
        text.Count.ShouldBe(count);
        text.Context.ShouldBeNull();
        text.Args.ShouldBe([count, "Walker"]);
    }

    [Fact]
    public void Plain_AndFormat_AreNotCounted()
    {
        UiText.Plain("Delete").Plural.ShouldBeNull();
        var nested = UiText.Counted("{0} node", "{0} nodes", 2);

        var text = UiText.Format("{0} · {1}", nested, 7L);

        text.Plural.ShouldBeNull();
        text.Args.ShouldBe([nested, 7L]);
    }

    [Fact]
    public void InContext_KeepsTheTextUnderAContext()
    {
        var text = UiText.Counted("{0} beam", "{0} beams", 3).InContext("part");

        text.Context.ShouldBe("part");
        text.ShouldNotBe(UiText.Counted("{0} beam", "{0} beams", 3));
        text.ShouldBe(UiText.Counted("{0} beam", "{0} beams", 3).InContext("part"));
    }

    [Fact]
    public void Equality_ComparesArgumentsIncludingNestedText()
    {
        static UiText Card(int nodes) => UiText.Format("{0} · {1}", UiText.Counted("{0} node", "{0} nodes", nodes), "Walker");

        Card(2).ShouldBe(Card(2));
        Card(2).GetHashCode().ShouldBe(Card(2).GetHashCode());
        Card(2).ShouldNotBe(Card(3));
    }

    [Fact]
    public void Args_AreCopied()
    {
        object[] args = ["Walker"];
        var text = UiText.Format("Train {0}", args);

        args[0] = "Crawler";

        text.Args.ShouldBe(["Walker"]);
    }

    [Theory]
    [InlineData("Train {1}")]
    [InlineData("Train")]
    [InlineData("Train {0} and {1}")]
    [InlineData("Train {0:0.0}")]
    [InlineData("Train {0,5}")]
    [InlineData("Train {0} {")]
    [InlineData("Train {0} }")]
    [InlineData("Train {x} {0}")]
    [InlineData("}{{} {0}")]
    public void Format_RejectsPlaceholdersThatDoNotMatchTheArguments(string message) =>
        Should.Throw<ArgumentException>(() => UiText.Format(message, "Walker"));

    [Fact]
    public void Format_AllowsEscapedBraces() =>
        UiText.Format("{{{0}}}", "Walker").Message.ShouldBe("{{{0}}}");

    [Fact]
    public void Counted_RejectsAPluralWithOtherPlaceholdersThanItsSingular() =>
        Should.Throw<ArgumentException>(() => UiText.Counted("{0} node of {1}", "{0} nodes", 2, "Walker"));

    [Fact]
    public void Constructors_RejectEmptyTextAndUnsupportedArguments()
    {
        Should.Throw<ArgumentException>(() => UiText.Plain(string.Empty));
        Should.Throw<ArgumentException>(() => UiText.Counted("{0} node", string.Empty, 2));
        Should.Throw<ArgumentException>(() => UiText.Plain("Delete").InContext(string.Empty));
        Should.Throw<ArgumentException>(() => UiText.Format("{0} m", 1.5));
    }

    [Fact]
    public void Number_IsTheNumberAlone()
    {
        UiText.Number(8).ShouldBe(UiText.Format("{0}", 8));
        UiText.Number(new FixedNumber(2.5, 1)).Args.ShouldBe([new FixedNumber(2.5, 1)]);
    }
}
