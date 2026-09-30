using System;

namespace Emberfield.Simulation
{
    [Flags]
    public enum CombatTags
    {
        None = 0, Worker = 1, Infantry = 2, Cavalry = 4, Ranged = 8,
        Light = 16, Heavy = 32, Structure = 64, Creature = 128, Siege = 256, Naval = 512
    }

    [Serializable]
    public sealed class AttackDefinition
    {
        public int Damage;
        public int RangeMillimetres = 1000;
        public int AcquireRangeMillimetres = 6000;
        public int CooldownTicks = 20;
        public int ProjectileSpeedMillimetresPerSecond;
        public int ProjectileLifetimeTicks = 400;
        public int SplashRadiusMillimetres;
        public int SplashDamagePermille;
        public DamageBonus[] Bonuses = Array.Empty<DamageBonus>();
    }

    [Serializable]
    public sealed class DamageBonus
    {
        public CombatTags TargetTags;
        public int MultiplierPermille = 1000;
    }
}
