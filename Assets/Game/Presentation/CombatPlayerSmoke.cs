using System.Collections;
using System.Collections.Generic;
using System.IO;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    // Invoked only by PlayerSmoke's explicit development-player command-line flag.
    internal static class CombatPlayerSmoke
    {
        internal static IEnumerator Run(MatchController match, string folder)
        {
            Directory.CreateDirectory(folder);
            match.enabled = false;
            yield return null;
            while (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            match.Select(new[] { 10, 11, 12, 13, 14, 15 });
            match.World.TryGetUnit(20, out var target);
            match.Tap(match.Rig.Camera.WorldToScreenPoint(DefinitionLoader.ToWorld(target.Position) + Vector3.up * .6f), false);
            bool accepted = true;
            foreach (int id in match.Selection)
                accepted &= match.World.TryGetUnit(id, out var unit) && unit.AttackTargetId == target.Id;
            bool projectiles = false, damage = false, captured = false, pixels = false;
            int initialCount = match.World.Units.Count, peakProjectiles = 0;
            var attackedRoles = new HashSet<string>();
            // Two fixed ticks per rendered frame: explicit acceleration, not a frame-time benchmark.
            for (int tick = 0; tick < World.TickRate * 60; tick++)
            {
                match.World.Tick();
                foreach (var unit in match.World.Units)
                {
                    if (unit.AttackCooldownTicks > 0) attackedRoles.Add(unit.DefinitionId);
                    damage |= unit.Health < unit.MaxHealth;
                }
                peakProjectiles = Mathf.Max(peakProjectiles, match.World.Projectiles.Count);
                projectiles |= match.World.Projectiles.Count > 0;
                if (tick % 2 != 0) continue;
                match.SyncPresentation(1);
                if (!captured && damage && match.View.ProjectileCount > 0 && attackedRoles.Count >= 3)
                {
                    match.SetFeedback("Battle drill: spears resist cavalry, cavalry closes on archers, archers pressure spears.");
                    match.Hud.Refresh(); match.Hud.PrepareOffscreenCapture(match.Rig.Camera);
                    yield return new WaitForEndOfFrame();
                    pixels = PlayerSmoke.Capture(match, Path.Combine(folder, "greybox.png"));
                    captured = true;
                }
                if (captured && match.World.DeathCount >= 3 && attackedRoles.Contains("reedguard") && attackedRoles.Contains("stringwarden") && attackedRoles.Contains("strider")) break;
                yield return null;
            }
            match.SyncPresentation(1);
            bool removed = match.World.Units.Count < initialCount && match.View.Count == match.World.Units.Count + match.World.Buildings.Count + match.World.Resources.Count;
            bool passed = accepted && projectiles && damage && captured && pixels && removed && attackedRoles.Contains("reedguard") && attackedRoles.Contains("stringwarden") && attackedRoles.Contains("strider");
            if (!captured)
            {
                match.Hud.PrepareOffscreenCapture(match.Rig.Camera);
                yield return new WaitForEndOfFrame();
                PlayerSmoke.Capture(match, Path.Combine(folder, "greybox.png"));
            }
            File.WriteAllText(Path.Combine(folder, "smoke.txt"), $"Passed: {passed}\nScenario: CombatSandbox\nUnity: {Application.unityVersion}\nResolution: {Screen.width}x{Screen.height}\nGraphics: {SystemInfo.graphicsDeviceName}\nAttackTapAccepted: {accepted}\nDamageObserved: {damage}\nProjectilesObserved: {projectiles}\nPeakProjectiles: {peakProjectiles}\nReedguardAttacked: {attackedRoles.Contains("reedguard")}\nStringwardenAttacked: {attackedRoles.Contains("stringwarden")}\nStriderAttacked: {attackedRoles.Contains("strider")}\nDeaths: {match.World.DeathCount}\nDeadViewsRemoved: {removed}\nTick: {match.World.TickIndex}\nEntities: {match.View.Count}\nCombatTicksAccelerated: True\nScreenshotCapturedDuringBattle: {captured}\n");
            Debug.Log("EMBERFIELD_COMBAT_SMOKE " + passed);
            Application.Quit(passed ? 0 : 1);
        }
    }
}
