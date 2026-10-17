using NodeRunner.ML.Ga;

namespace NodeRunner.ML.Tests;

public sealed class ParallelEvaluationScheduleTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(20, 16)]
    [InlineData(32, 32)]
    public void EveryCandidateIsEvaluatedExactlyOnce(int populationSize, int slotCount)
    {
        var schedule = new ParallelEvaluationSchedule(populationSize, slotCount);
        var evaluated = new List<int>();
        for (var slot = 0; slot < slotCount; slot++)
        {
            schedule.TryAssignNext(slot, out var candidate).ShouldBeTrue();
            evaluated.Add(candidate);
        }

        // With a slot per candidate the whole generation starts at once.
        evaluated.ShouldBe(Enumerable.Range(0, slotCount));
        while (!schedule.IsComplete)
        {
            for (var slot = slotCount - 1; slot >= 0; slot--)
            {
                if (schedule.ActiveCandidate(slot) < 0)
                {
                    continue;
                }

                schedule.Complete(slot);
                if (schedule.TryAssignNext(slot, out var candidate))
                {
                    evaluated.Add(candidate);
                }
            }
        }

        evaluated.Order().ShouldBe(Enumerable.Range(0, populationSize));
        schedule.CompletedCount.ShouldBe(populationSize);
    }

    [Fact]
    public void Reset_ReplaysFromTheFirstCandidate_AndBadTransitionsThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ParallelEvaluationSchedule(0, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => new ParallelEvaluationSchedule(2, 3));
        var schedule = new ParallelEvaluationSchedule(populationSize: 4, slotCount: 2);
        Should.Throw<InvalidOperationException>(() => schedule.Complete(0));
        schedule.TryAssignNext(0, out _);
        schedule.TryAssignNext(1, out _);
        Should.Throw<InvalidOperationException>(() => schedule.TryAssignNext(0, out _));
        Should.Throw<ArgumentOutOfRangeException>(() => schedule.ActiveCandidate(2));
        schedule.Complete(0);

        schedule.Reset();

        schedule.CompletedCount.ShouldBe(0);
        schedule.TryAssignNext(0, out var first).ShouldBeTrue();
        first.ShouldBe(0);
    }
}
