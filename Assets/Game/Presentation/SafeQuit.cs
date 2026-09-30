using System.Collections;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Quits once the frame in flight has been presented and two more frames have passed. Quitting in
    /// the same frame as a capture or heavy rendering could tear Direct3D 12 down while its graphics jobs
    /// still held resources, and the player then crashed on exit.
    /// </summary>
    public sealed class SafeQuit : MonoBehaviour
    {
        private int code;

        public static void Request(int exitCode = 0)
        {
            var runner = new GameObject("Safe quit").AddComponent<SafeQuit>();
            runner.code = exitCode;
            DontDestroyOnLoad(runner.gameObject);
        }

        private IEnumerator Start()
        {
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return null;
            Application.Quit(code);
        }
    }
}
