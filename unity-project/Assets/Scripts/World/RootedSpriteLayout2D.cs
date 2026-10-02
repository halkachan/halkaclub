using System;
using UnityEngine;

namespace Halka.Game.World
{
    public static class RootedSpriteLayout2D
    {
        // A centered sprite spans the root tile and the tiles above it.
        public static Vector3 OffsetFromBottomCenter(Sprite sprite)
        {
            if (sprite == null || sprite.pixelsPerUnit <= 0f)
                throw new ArgumentException("A valid sprite is required", nameof(sprite));
            var height = sprite.rect.height / sprite.pixelsPerUnit;
            return Vector3.up * ((height - GridWorld2D.TileWorldSize) * 0.5f);
        }
    }
}
