using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D.World
{
    // ver3.2, test only: the definitions a generated test world is made of (an asset under
    // Assets/POC2D3D/Fixtures, referenced only by the chunk test scene and the checks, so it never
    // reaches the release build's data).
    public sealed class PocTestWorldFixture : ScriptableObject
    {
        public SurfaceDefinition grass;
        public SurfaceDefinition dirt;
        public WorldObjectDefinition tree;
        public WorldObjectDefinition stone;
        public WorldObjectDefinition flower;
        public WorldObjectDefinition bench;
        public WorldObjectDefinition well;
        public WorldObjectDefinition house;
        public EntityDefinition player;
        public EntityDefinition crow;

        public bool IsComplete => grass != null && dirt != null && tree != null && stone != null && flower != null &&
            bench != null && well != null && house != null && player != null && crow != null;
    }
}
