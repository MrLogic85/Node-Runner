using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiNotificationQueueTests
{
    private static UiNotificationSpec Spec(string title, string? id = null, bool replaceable = false) =>
        new(UiPopupType.Default, title, string.Empty) { Id = id, Replaceable = replaceable };

    private static string[] Titles(UiNotificationQueue queue) => [.. queue.Waiting.Select(spec => spec.Title)];

    [Fact]
    public void WithoutIds_NotificationsShowInTurn()
    {
        var queue = new UiNotificationQueue();

        queue.Offer(Spec("A")).ShouldBe(UiNotificationOffer.Queued);
        queue.Offer(Spec("A")).ShouldBe(UiNotificationOffer.Queued);
        queue.TryAdvance(out var shown).ShouldBeTrue();

        shown!.Title.ShouldBe("A");
        queue.Current.ShouldBeSameAs(shown);
        Titles(queue).ShouldBe(["A"]);
    }

    [Fact]
    public void AnIdAlreadyWaiting_IsNotQueuedAgain()
    {
        var queue = new UiNotificationQueue();
        queue.Offer(Spec("Busy"));
        queue.TryAdvance(out _);

        queue.Offer(Spec("Locked", "lock")).ShouldBe(UiNotificationOffer.Queued);
        queue.Offer(Spec("Locked", "lock")).ShouldBe(UiNotificationOffer.AlreadyWaiting);

        Titles(queue).ShouldBe(["Locked"]);
    }

    [Fact]
    public void TheShowingId_RepeatsInsteadOfQueueing()
    {
        var queue = new UiNotificationQueue();
        queue.Offer(Spec("Locked", "lock"));
        queue.TryAdvance(out _);

        queue.Offer(Spec("Locked", "lock")).ShouldBe(UiNotificationOffer.RepeatsCurrent);

        queue.Waiting.ShouldBeEmpty();
    }

    [Fact]
    public void AClosedId_CanShowAgain()
    {
        var queue = new UiNotificationQueue();
        queue.Offer(Spec("Locked", "lock"));
        queue.TryAdvance(out _);
        queue.CloseCurrent();

        queue.Offer(Spec("Locked", "lock")).ShouldBe(UiNotificationOffer.Queued);
    }

    [Fact]
    public void AReplaceableShowingNotification_GivesWayToANewerOne()
    {
        var queue = new UiNotificationQueue();
        queue.Offer(Spec("Stepper", "stepper", replaceable: true));
        queue.TryAdvance(out _);

        queue.Offer(Spec("Wing", "wing", replaceable: true)).ShouldBe(UiNotificationOffer.ReplacesCurrent);
        queue.CloseCurrent();
        queue.TryAdvance(out var shown);

        shown!.Title.ShouldBe("Wing");
    }

    [Fact]
    public void AStayingShowingNotification_KeepsItsTurn()
    {
        var queue = new UiNotificationQueue();
        queue.Offer(Spec("Saved"));
        queue.TryAdvance(out _);

        queue.Offer(Spec("Wing", "wing", replaceable: true)).ShouldBe(UiNotificationOffer.Queued);
    }

    [Fact]
    public void ReplaceableWaitingNotifications_LeaveTheQueueWhenANewerOneArrives()
    {
        var queue = new UiNotificationQueue();
        queue.Offer(Spec("Saved"));
        queue.TryAdvance(out _);
        queue.Offer(Spec("Stepper", "stepper", replaceable: true));
        queue.Offer(Spec("Copy failed"));
        queue.Offer(Spec("Wing", "wing", replaceable: true));

        queue.Offer(Spec("Save failed")).ShouldBe(UiNotificationOffer.Queued);

        Titles(queue).ShouldBe(["Copy failed", "Save failed"]);
    }

    [Fact]
    public void ADroppedDuplicate_DoesNotReplaceAnything()
    {
        var queue = new UiNotificationQueue();
        queue.Offer(Spec("Stepper", "stepper", replaceable: true));
        queue.TryAdvance(out _);

        queue.Offer(Spec("Stepper", "stepper", replaceable: true)).ShouldBe(UiNotificationOffer.RepeatsCurrent);

        queue.Current!.Title.ShouldBe("Stepper");
    }

    [Fact]
    public void PushFront_GoesFirst()
    {
        var queue = new UiNotificationQueue();
        queue.Offer(Spec("A"));
        queue.Offer(Spec("B"));

        queue.PushFront(Spec("Action failed"));

        Titles(queue).ShouldBe(["Action failed", "A", "B"]);
    }

    [Fact]
    public void Clear_EmptiesTheQueueAndTheShowingSlot()
    {
        var queue = new UiNotificationQueue();
        queue.Offer(Spec("A"));
        queue.TryAdvance(out _);
        queue.Offer(Spec("B"));

        queue.Clear();

        queue.Current.ShouldBeNull();
        queue.Waiting.ShouldBeEmpty();
        queue.TryAdvance(out _).ShouldBeFalse();
    }
}
