using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class UiTextTests
{
    [Fact]
    public void Counted_KeepsBothFormsAndPassesTheCountAsTheFirstArgument_PlainAndFormatAreNotCounted()
    {
        var text = UiText.Counted("Trained {0} generation for {1}", "Trained {0} generations for {1}", 2, "Walker");

        text.Message.ShouldBe("Trained {0} generation for {1}");
        text.Plural.ShouldBe("Trained {0} generations for {1}");
        text.Count.ShouldBe(2);
        text.Context.ShouldBeNull();
        text.Args.ShouldBe([2, "Walker"]);
        UiText.Plain("Delete").Plural.ShouldBeNull();
        UiText.Format("{0} · {1}", text, 7L).Plural.ShouldBeNull();
    }

    [Fact]
    public void Equality_ComparesContextAndArgumentsIncludingNestedText_WhichAreCopied()
    {
        static UiText Card(int nodes) => UiText.Format("{0} · {1}", UiText.Counted("{0} node", "{0} nodes", nodes), "Walker");
        object[] args = ["Walker"];
        var train = UiText.Format("Train {0}", args);
        args[0] = "Crawler";

        Card(2).ShouldBe(Card(2));
        Card(2).GetHashCode().ShouldBe(Card(2).GetHashCode());
        Card(2).ShouldNotBe(Card(3));
        Card(2).InContext("part").Context.ShouldBe("part");
        Card(2).InContext("part").ShouldNotBe(Card(2));
        Card(2).InContext("part").ShouldBe(Card(2).InContext("part"));
        train.Args.ShouldBe(["Walker"]);
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
    public void Constructors_RejectEmptyTextUnsupportedArgumentsAndAPluralWithOtherPlaceholders()
    {
        Should.Throw<ArgumentException>(() => UiText.Plain(string.Empty));
        Should.Throw<ArgumentException>(() => UiText.Counted("{0} node", string.Empty, 2));
        Should.Throw<ArgumentException>(() => UiText.Plain("Delete").InContext(string.Empty));
        Should.Throw<ArgumentException>(() => UiText.Format("{0} m", 1.5));
        Should.Throw<ArgumentException>(() => UiText.Counted("{0} node of {1}", "{0} nodes", 2, "Walker"));
    }

    [Fact]
    public void NumberAndAsWritten_AreTheValueAloneAsAnArgument_SoTheTextIsNeverTranslated()
    {
        UiText.Number(8).ShouldBe(UiText.Format("{0}", 8));
        UiText.Number(new FixedNumber(2.5, 1)).Args.ShouldBe([new FixedNumber(2.5, 1)]);
        var name = UiText.AsWritten("Thigh");
        name.Message.ShouldBe("{0}");
        name.Args.ShouldBe(["Thigh"]);
    }
}
