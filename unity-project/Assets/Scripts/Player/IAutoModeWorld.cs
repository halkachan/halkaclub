using Halka.Game.World;
using UnityEngine;

namespace Halka.Game.Player
{
    // Everything AutoModeController reads from, or asks of, the world it walks in. The 2D game
    // answers from its existing components (AutoModeController's default); the shared 2D/3D
    // world of ver3.0 answers from its own Grid and Player state, so both run the same AUTO.
    public interface IAutoModeWorld
    {
        // The registered active map, or (without a map registry) the house fallback of the 2D game.
        string ActiveMapId { get; }
        // Indoor AUTO needs the map registry (the room's Map Data); without it AUTO stays outdoors.
        bool HasMapRegistry { get; }
        MapDefinition GetMap(string mapId);

        bool HasPlayer { get; }
        Vector2Int PlayerCell { get; }
        bool PlayerIsMoving { get; }
        bool PlayerMovementLocked { get; }
        // A step in a world (East/West/North/South) direction, exactly as a manual step.
        bool TryStep(Vector2Int direction);
        void CancelStep();

        bool HasActiveMessage { get; }
        bool HasHouse { get; }
        Vector2Int HouseDoorCell { get; }
        Vector2Int OutsideEntryCell { get; }
        // Static Grid blocking, then the dynamic occupancy (the crow on the ground).
        bool CanEnter(Vector2Int cell);
        bool CanPlayerEnter(Vector2Int cell);
        bool TryGetCrowCell(out Vector2Int cell);
        // True while play is suspended outside the browser's own focus rules (for example the
        // phone rotation guide): AUTO then behaves exactly as when the tab is in the background.
        bool Suspended { get; }
    }
}
