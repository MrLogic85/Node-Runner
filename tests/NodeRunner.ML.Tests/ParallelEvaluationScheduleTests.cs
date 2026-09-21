using NodeRunner.ML.Ga;

namespace NodeRunner.ML.Tests;

public sealed class ParallelEvaluationScheduleTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(20, 16)]
    public void AssignAndComplete_EvaluatesEveryCandidateExactlyOnce(int populationSize, int slotCount)
    {
        var schedule = new ParallelEvaluationSchedule(populationSize, slotCount);
        var evaluated = new List<int>();

        for (var slot = 0; slot < slotCount; slot++)
        {
            if (schedule.TryAssignNext(slot, out var candidate))
            {
                evaluated.Add(candidate);
            }
        }

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
        evaluated.Distinct().Count().ShouldBe(populationSize);
        schedule.CompletedCount.ShouldBe(populationSize);
        schedule.LowestActiveCandidate.ShouldBe(-1);
    }

    [Fact]
    public void Reset_ReplaysAssignmentFromFirstCandidate()
    {
        var schedule = new ParallelEvaluationSchedule(populationSize: 4, slotCount: 2);
        schedule.TryAssignNext(0, out _);
        schedule.TryAssignNext(1, out _);
        schedule.Complete(0);

        schedule.Reset();

        schedule.CompletedCount.ShouldBe(0);
        schedule.TryAssignNext(0, out var firstCandidate).ShouldBeTrue();
        firstCandidate.ShouldBe(0);
        schedule.LowestActiveCandidate.ShouldBe(0);
    }

    [Fact]
    public void InvalidTransitions_Throw()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ParallelEvaluationSchedule(0, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => new ParallelEvaluationSchedule(2, 3));

        var schedule = new ParallelEvaluationSchedule(populationSize: 2, slotCount: 1);
        Should.Throw<InvalidOperationException>(() => schedule.Complete(0));

        schedule.TryAssignNext(0, out _);

        Should.Throw<InvalidOperationException>(() => schedule.TryAssignNext(0, out _));
        Should.Throw<ArgumentOutOfRangeException>(() => schedule.ActiveCandidate(1));
    }
}
