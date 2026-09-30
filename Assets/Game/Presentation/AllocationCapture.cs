using UnityEngine;
using UnityEngine.Profiling;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Development players only: records the Unity profiler to a binary log, with the managed callstack of every GC
    /// allocation, over a window of frames a probe chooses (<c>-emberfieldAllocationCapture &lt;frames&gt;</c>). The
    /// editor reads the log back into a table of what allocated and from where (tools/Report-Allocations.ps1). The
    /// profiler slows every frame it records, so a run that captures is for finding allocations, never for timings.
    /// </summary>
    public static class AllocationCapture
    {
        private static int remaining;
        public static bool IsRecording => remaining > 0;

        /// <summary>Frames to record from the command line, or 0 when none were asked for (or not a development player).</summary>
        public static int RequestedFrames(string[] args)
        {
            if (!Debug.isDebugBuild) return 0;
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-emberfieldAllocationCapture" && int.TryParse(args[i + 1], out int frames)) return Mathf.Clamp(frames, 1, 2000);
            return 0;
        }

        public static void Begin(string file, int frames)
        {
            if (!Debug.isDebugBuild || frames <= 0 || !Profiler.supported) return;
            Profiler.maxUsedMemory = 512 * 1024 * 1024;
            Profiler.logFile = file;
            Profiler.enableBinaryLog = true;
            Profiler.enableAllocationCallstacks = true;
            Profiler.enabled = true;
            remaining = frames;
            Debug.Log("EMBERFIELD_ALLOCATION_CAPTURE begin " + frames + " frames: " + file);
        }

        /// <summary>Called once per recorded frame; closes the log after the requested count.</summary>
        public static void Frame()
        {
            if (remaining <= 0 || --remaining > 0) return;
            Stop();
        }

        public static void Stop()
        {
            remaining = 0;
            if (!Profiler.enabled) return;
            Profiler.enabled = false;
            Profiler.enableAllocationCallstacks = false;
            Profiler.enableBinaryLog = false;
            Profiler.logFile = "";
            Debug.Log("EMBERFIELD_ALLOCATION_CAPTURE end");
        }
    }
}
