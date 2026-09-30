using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace Emberfield.Editor
{
    /// <summary>
    /// Reads a development player's allocation capture (AllocationCapture, <c>-emberfieldAllocationCapture</c>) and
    /// writes what allocated per recorded frame: grouped by the first game method on each allocation's managed
    /// callstack, by the whole callstack, and by the profiler marker it happened under. Batch entry point for
    /// tools/Report-Allocations.ps1: <c>-allocationCapture &lt;file.raw&gt; -allocationReport &lt;file.txt&gt;</c>.
    /// </summary>
    public static class AllocationReport
    {
        private sealed class Site { public long Bytes; public int Count; }

        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            string input = Argument(args, "-allocationCapture"), output = Argument(args, "-allocationReport");
            if (input == null || output == null || !File.Exists(input)) throw new ArgumentException("Pass -allocationCapture <existing .raw> and -allocationReport <.txt>.");
            ProfilerDriver.ClearAllFrames();
            if (!ProfilerDriver.LoadProfile(input, false)) throw new InvalidOperationException("The profiler could not load " + input);
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
            var byMethod = new Dictionary<string, Site>();
            var byStack = new Dictionary<string, Site>();
            var byMarker = new Dictionary<string, Site>();
            var names = new Dictionary<ulong, string>();
            var callstack = new List<ulong>();
            var parents = new List<KeyValuePair<int, string>>();
            var owners = new List<double[]>();
            var times = new Dictionary<string, double[]>();
            var perFrame = new List<long>();
            var perFrameCount = new List<int>();
            for (int frame = first; frame <= last; frame++)
            {
                long frameBytes = 0; int frameCount = 0;
                for (int thread = 0; thread < 256; thread++)
                {
                    using (var view = ProfilerDriver.GetRawFrameDataView(frame, thread))
                    {
                        if (view == null || !view.valid) break;
                        int alloc = view.GetMarkerId("GC.Alloc");
                        if (alloc == FrameDataView.invalidMarkerId) continue;
                        parents.Clear(); owners.Clear();
                        for (int i = 0; i < view.sampleCount; i++)
                        {
                            while (parents.Count > 0 && parents[parents.Count - 1].Key < i) { parents.RemoveAt(parents.Count - 1); owners.RemoveAt(owners.Count - 1); }
                            if (view.GetSampleMarkerId(i) != alloc)
                            {
                                string sampleName = view.GetSampleName(i);
                                // Main-thread time by marker, inclusive and self, so the capture also says where the frame went.
                                if (thread == 0)
                                {
                                    double inclusive = view.GetSampleTimeMs(i);
                                    if (!times.TryGetValue(sampleName, out var time)) times.Add(sampleName, time = new double[2]);
                                    time[0] += inclusive; time[1] += inclusive;
                                    if (owners.Count > 0) owners[owners.Count - 1][1] -= inclusive;
                                    owners.Add(time);
                                }
                                else owners.Add(new double[2]);
                                parents.Add(new KeyValuePair<int, string>(i + view.GetSampleChildrenCountRecursive(i), sampleName));
                                continue;
                            }
                            long size = view.GetSampleMetadataCount(i) > 0 ? view.GetSampleMetadataAsLong(i, 0) : 0;
                            frameBytes += size; frameCount++;
                            string marker = view.threadName + " | " + string.Join(" > ", parents.Skip(Math.Max(0, parents.Count - 4)).Select(p => p.Value));
                            Add(byMarker, marker, size);
                            view.GetSampleCallstack(i, callstack);
                            string game = null;
                            var stackFrames = new List<string>();
                            foreach (ulong address in callstack)
                            {
                                if (!names.TryGetValue(address, out string name))
                                {
                                    var method = view.ResolveMethodInfo(address);
                                    name = string.IsNullOrEmpty(method.methodName) ? "0x" + address.ToString("x") :
                                        method.methodName + (string.IsNullOrEmpty(method.sourceFileName) ? "" : " (" + Path.GetFileName(method.sourceFileName) + ":" + method.sourceFileLine + ")");
                                    names.Add(address, name);
                                }
                                // Native frames (the allocator's own) resolve to no method; the managed ones name the site.
                                if (stackFrames.Count < 8 && !name.StartsWith("0x", StringComparison.Ordinal)) stackFrames.Add(name);
                                if (game == null && name.StartsWith("Emberfield.", StringComparison.Ordinal)) game = name;
                            }
                            Add(byMethod, game ?? (stackFrames.Count > 0 ? stackFrames[0] : "(no managed callstack) " + marker), size);
                            Add(byStack, stackFrames.Count > 0 ? string.Join("\n      <- ", stackFrames) : "(no managed callstack) " + marker, size);
                        }
                    }
                }
                perFrame.Add(frameBytes); perFrameCount.Add(frameCount);
            }
            int recorded = Math.Max(1, perFrame.Count);
            var text = new StringBuilder();
            text.AppendLine("Allocation capture: " + input);
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "Frames {0}; total {1:N0} B in {2:N0} allocations; per frame mean {3:N0} B / {4:N1} allocations; median {5:N0} B; max {6:N0} B; frames with none {7}.",
                perFrame.Count, perFrame.Sum(), perFrameCount.Sum(), perFrame.Sum() / (double)recorded, perFrameCount.Sum() / (double)recorded,
                Median(perFrame), perFrame.Count > 0 ? perFrame.Max() : 0, perFrame.Count(b => b == 0)));
            Write(text, "By first game method", byMethod, recorded, 60);
            Write(text, "By profiler marker (thread | last four parents)", byMarker, recorded, 40);
            Write(text, "By callstack (innermost first)", byStack, recorded, 200);
            // Recording callstacks slows every allocation, so these times are for ranking, not for budgets.
            text.AppendLine().AppendLine("== Main thread markers by self time (ms/frame, inclusive in brackets) ==");
            foreach (var pair in times.OrderByDescending(p => p.Value[1]).Take(50))
                text.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,9:N3} ms [{1,9:N3}]  {2}", pair.Value[1] / recorded, pair.Value[0] / recorded, pair.Key));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, text.ToString());
            ProfilerDriver.ClearAllFrames();
        }

        private static void Add(Dictionary<string, Site> sites, string key, long size)
        {
            if (!sites.TryGetValue(key, out var site)) sites.Add(key, site = new Site());
            site.Bytes += size; site.Count++;
        }

        private static void Write(StringBuilder text, string title, Dictionary<string, Site> sites, int frames, int limit)
        {
            text.AppendLine().AppendLine("== " + title + " ==");
            foreach (var pair in sites.OrderByDescending(p => p.Value.Bytes).Take(limit))
                text.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,10:N1} B/frame {1,8:N2} allocs/frame  {2}", pair.Value.Bytes / (double)frames, pair.Value.Count / (double)frames, pair.Key));
        }

        private static long Median(List<long> values)
        {
            if (values.Count == 0) return 0;
            var sorted = values.OrderBy(v => v).ToList();
            return sorted[sorted.Count / 2];
        }

        private static string Argument(string[] args, string name)
        {
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
