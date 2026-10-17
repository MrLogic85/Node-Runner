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

    /// <summary>An id already waiting is not queued again, the showing id repeats instead, and a closed id can show again.</summary>
    [Fact]
    public void AnId_IsQueuedOnce_RepeatsWhileShowing_AndShowsAgainOnceClosed()
    {
        var queue = new UiNotificationQueue();
        queue.Offer(Spec("Busy"));
        queue.TryAdvance(out _);
        queue.Offer(Spec("Locked", "lock")).ShouldBe(UiNotificationOffer.Queued);
        queue.Offer(Spec("Locked", "lock")).ShouldBe(UiNotificationOffer.AlreadyWaiting);
        Titles(queue).ShouldBe(["Locked"]);

        queue.CloseCurrent();
        queue.TryAdvance(out _);
        queue.Offer(Spec("Locked", "lock")).ShouldBe(UiNotificationOffer.RepeatsCurrent);
        queue.Waiting.ShouldBeEmpty();

        queue.CloseCurrent();
        queue.Offer(Spec("Locked", "lock")).ShouldBe(UiNotificationOffer.Queued);
    }

    /// <summary>A replaceable showing notification gives way to a newer one, unless that is its own duplicate; a staying one keeps its turn.</summary>
    [Fact]
    public void AReplaceableShowingNotification_GivesWayToANewerOne()
    {
        var queue = new UiNotificationQueue();
        queue.Offer(Spec("Stepper", "stepper", replaceable: true));
        queue.TryAdvance(out _);
        queue.Offer(Spec("Stepper", "stepper", replaceable: true)).ShouldBe(UiNotificationOffer.RepeatsCurrent);
        queue.Offer(Spec("Wing", "wing", replaceable: true)).ShouldBe(UiNotificationOffer.ReplacesCurrent);
        queue.CloseCurrent();
        queue.TryAdvance(out var shown);
        shown!.Title.ShouldBe("Wing");

        var staying = new UiNotificationQueue();
        staying.Offer(Spec("Saved"));
        staying.TryAdvance(out _);
        staying.Offer(Spec("Wing", "wing", replaceable: true)).ShouldBe(UiNotificationOffer.Queued);
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
