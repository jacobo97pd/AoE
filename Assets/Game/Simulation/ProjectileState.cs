namespace Emberfield.Simulation
{
    public sealed class ProjectileState
    {
        public int Id { get; }
        public int OwnerId { get; }
        public int SourceUnitId { get; }
        public int SourceEntityId => SourceUnitId;
        public int TargetEntityId { get; }
        public SimPoint Position { get; internal set; }
        public SimPoint PreviousPosition { get; internal set; }
        public int RemainingLifetimeTicks { get; internal set; }
        internal int Damage { get; }
        internal int Speed { get; }
        internal int SpeedRemainder;
        internal AttackDefinition SplashAttack;
        internal int SplashBaseDamage;
        internal ProjectileState(int id, UnitState source, int targetId, int damage, int speed, int lifetime, AttackDefinition attack = null)
        {
            Id = id; OwnerId = source.OwnerId; SourceUnitId = source.Id; TargetEntityId = targetId;
            Position = PreviousPosition = source.Position; Damage = damage; Speed = speed;
            RemainingLifetimeTicks = lifetime;
            SplashAttack = attack; SplashBaseDamage = source.AttackDamage;
        }
        internal ProjectileState(int id, BuildingState source, int targetId, int damage, int speed, int lifetime)
        {
            Id = id; OwnerId = source.OwnerId; SourceUnitId = source.Id; TargetEntityId = targetId;
            Position = PreviousPosition = source.Position; Damage = damage; Speed = speed;
            RemainingLifetimeTicks = lifetime;
        }
    }
}
