using System.Text;
using Emberfield.Simulation;

namespace Emberfield.Presentation
{
    public sealed partial class MatchHud
    {
        // Board and Land are orders, so they sit with the selection's other orders on the context row.
        private void AddNavalUnitActions()
        {
            if (match.BoardingTroops().Length > 0 && match.HasShipWithRoom()) Button(contextRow, "Board ship\nTap one of your ships", match.ChooseShip);
            if (match.LoadedShips().Length > 0) Button(contextRow, "Land troops\nTap the shore", match.ChooseLanding);
        }

        // Counted in place, not through the order helpers' lists: the key is written on every refresh.
        private void AppendNavalContextKey(StringBuilder key)
        {
            int troops = 0, loaded = 0;
            for (int i = 0; i < match.Selection.Count; i++)
                if (match.World.TryGetUnit(match.Selection[i], out var unit) && unit.OwnerId == MatchController.LocalPlayer)
                {
                    if (MatchController.CanBoardShip(unit)) troops++;
                    if (unit.CargoCount > 0) loaded++;
                }
            key.Append(":naval:").Append(match.ChoosingShip).Append(':').Append(match.ChoosingLanding).Append(':');
            AppendNumber(key, troops).Append(':').Append(troops > 0 && match.HasShipWithRoom()).Append(':');
            AppendNumber(key, loaded);
        }

        private string NavalUnitDetails(UnitState unit)
        {
            if (unit.CargoCapacity > 0)
                return " · " + unit.CargoCount + "/" + unit.CargoCapacity + " aboard" + (unit.IsUnloading ? " · Sailing to land them" : "");
            if (unit.EmbarkShipId != 0) return unit.EmbarkRemainingTicks > 0 ? " · Climbing aboard" : " · Heading for the ship";
            return "";
        }
    }
}
