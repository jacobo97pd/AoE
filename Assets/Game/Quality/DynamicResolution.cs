using System;

namespace Emberfield.Quality
{
    /// <summary>
    /// Lowers the 3D render scale while frames run over budget and raises it again once they have room, one step at
    /// a time. Feed it each frame: how long the frame's work took (CPU or GPU, whichever is longer; -1 when the device
    /// cannot say), how long the frame lasted on the wall clock, and how much time passed.
    /// </summary>
    /// <remarks>
    /// A phone holds its frame rate cap, so the wall clock cannot show spare time: a frame that finishes early still
    /// waits for its slot. It can only show frames that miss the cap. Stepping back up therefore needs the measured
    /// work time; without it the scale only goes down, which is the safe side. Every step up that is taken back
    /// within <see cref="RelapseSeconds"/> doubles the wait before the next one, so a device on the edge settles
    /// instead of flickering between two sizes.
    /// </remarks>
    public sealed class DynamicResolution
    {
        public const float Step = .1f;
        /// <summary>Work above this share of the budget, or wall time this far over it, counts as over budget.</summary>
        public const double OverWork = .92, OverWall = 1.15;
        /// <summary>Work below this share of the budget, with the cap held, counts as room to spare.</summary>
        public const double UnderWork = .7, HeldWall = 1.05;
        public const double OverSeconds = 2, UnderSeconds = 6, RelapseSeconds = 10, LongestWait = 60;
        /// <summary>Time constant of the smoothing, in seconds.</summary>
        public const double Smoothing = .5;

        private readonly double budget;
        private double work = -1, wall = -1, over, under, sinceUp = double.MaxValue, upWait = UnderSeconds;

        public float Scale { get; private set; }
        public float Maximum { get; }
        public float Minimum { get; }
        public double BudgetMilliseconds => budget;
        public double SmoothedWorkMilliseconds => work;
        public double SmoothedWallMilliseconds => wall;

        public DynamicResolution(float maximum, float minimum, int targetFrameRate)
        {
            if (targetFrameRate <= 0) throw new ArgumentOutOfRangeException(nameof(targetFrameRate));
            if (!(maximum > 0) || !(minimum > 0)) throw new ArgumentOutOfRangeException(nameof(minimum));
            Maximum = maximum; Minimum = Math.Min(minimum, maximum); Scale = maximum;
            budget = 1000.0 / targetFrameRate;
        }

        /// <returns>True when <see cref="Scale"/> changed.</returns>
        public bool Observe(double workMilliseconds, double wallMilliseconds, double elapsedSeconds)
        {
            if (!(elapsedSeconds > 0) || !(wallMilliseconds > 0) || double.IsInfinity(wallMilliseconds)) return false;
            // A hitch or a pause is one slow frame, not seconds of evidence: it counts for at most a quarter second
            // and twice the budget, so it cannot hold the average over the line on its own.
            elapsedSeconds = Math.Min(elapsedSeconds, .25);
            wallMilliseconds = Math.Min(wallMilliseconds, budget * 2);
            workMilliseconds = Math.Min(workMilliseconds, budget * 2);
            double blend = 1 - Math.Exp(-elapsedSeconds / Smoothing);
            wall = wall < 0 ? wallMilliseconds : wall + (wallMilliseconds - wall) * blend;
            bool measured = workMilliseconds > 0 && !double.IsInfinity(workMilliseconds);
            if (measured) work = work < 0 ? workMilliseconds : work + (workMilliseconds - work) * blend;
            else work = -1;
            if (sinceUp < double.MaxValue) sinceUp += elapsedSeconds;

            bool isOver = wall > budget * OverWall || (measured && work > budget * OverWork);
            bool hasRoom = measured && work < budget * UnderWork && wall < budget * HeldWall;
            if (isOver) { over += elapsedSeconds; under = 0; }
            else if (hasRoom) { under += elapsedSeconds; over = 0; }
            else { over = 0; under = 0; }

            if (over >= OverSeconds && Scale > Minimum)
            {
                if (sinceUp < RelapseSeconds) upWait = Math.Min(LongestWait, upWait * 2);
                Scale = Math.Max(Minimum, Round(Scale - Step)); over = 0; under = 0; sinceUp = double.MaxValue;
                return true;
            }
            if (under >= upWait && Scale < Maximum)
            {
                Scale = Math.Min(Maximum, Round(Scale + Step)); over = 0; under = 0; sinceUp = 0;
                return true;
            }
            return false;
        }

        // Keeps repeated steps on the same few sizes instead of drifting by float error.
        private static float Round(float scale) => (float)Math.Round(scale * 100) / 100;
    }
}
