using System;
using System.Collections.Generic;
using Halka.Game.Input;
using Halka.Game.UI;
using Halka.Game.World;
using Halka.Poc2D3D.Life;
using UnityEditor;
using UnityEngine;

namespace Halka.Poc2D3D.EditorTools
{
    // ver4.0: what the scenes get on top of ver3.9 (called by PocSceneBuilder after Phase 6): the life
    // controller (PocLife: clock, creator stations, ﾆｺﾁｭｰﾌﾞ, passers-by, Rin and Len, hide-and-seek),
    // the mini games window and the clock HUD, with the characters' models, materials and sprites.
    public static class PocSceneBuilderVer40
    {
        public const string Models = "Assets/POC2D3D/Models/V40/";
        public const string Sprites = "Assets/Content/Character/V40/";
        public const string RinMaterialPath = "Assets/POC2D3D/Materials/POC_V40_Rin.mat";
        public const string LenMaterialPath = "Assets/POC2D3D/Materials/POC_V40_Len.mat";
        public const string TextureTintShader = "HALKA/POC/ToonTextureTint";
        // Rin and Len are about 2.5-2.75 Blender units tall: 0.25 makes them about 0.65 m (ﾊﾙｶﾁｬﾝ is 0.97).
        public const float OneHeadScale = 0.25f;
        public static readonly string[] NpcIds = { "fan_male", "fan_female", "anti_male", "anti_female" };

        public static void Build(GameObject root)
        {
            var session = root.GetComponent<SharedWorldSession>();
            var gameplay = root.GetComponentInChildren<PocGameplay>(true);
            var menu = root.GetComponentInChildren<GameMenuController>(true);
            var hud = root.GetComponentInChildren<GameHud>(true);
            var worldTime = root.GetComponent<WorldTimeController>();
            var switcher = root.GetComponent<PocViewSwitcher>();
            var save = root.GetComponentInChildren<Save.HarukaSaveSystem>(true);
            var input = root.GetComponentInChildren<PocInput>(true);
            var font = AssetDatabase.LoadAssetAtPath<Font>(PocSceneBuilder.MessageFontPath);
            if (session == null || gameplay == null || menu == null || hud == null || worldTime == null || switcher == null || save == null || input == null)
                throw new InvalidOperationException("ver4.0: scene parts are missing");
            var lifeObject = new GameObject("ver4.0 そうさくせいかつ (PocLife)");
            lifeObject.transform.SetParent(root.transform, false);
            var games = lifeObject.AddComponent<PocMinigames>();
            games.ConfigureFont(font);
            var life = lifeObject.AddComponent<PocLife>();
            life.Configure(session, gameplay, menu, hud, worldTime, switcher, save, input, gameplay.FastTravel, gameplay.Crow, games, Looks());
            var clock = lifeObject.AddComponent<PocLifeHud>();
            clock.Configure(life, font);
            EditorUtility.SetDirty(lifeObject);
        }

        public static PocCharacterLook[] Looks()
        {
            var looks = new List<PocCharacterLook>
            {
                new PocCharacterLook
                {
                    id = PocLife.RinId, model = Load(Models + "rin_onehead.fbx"), material = TintMaterial(RinMaterialPath, Models + "rin_onehead_tex.png"),
                    scale = OneHeadScale, outlineId = 253, spriteOffsetNorth = -0.2f, sprites = SpriteSet("rin"),
                },
                new PocCharacterLook
                {
                    id = PocLife.LenId, model = Load(Models + "len_onehead.fbx"), material = TintMaterial(LenMaterialPath, Models + "len_onehead_tex.png"),
                    scale = OneHeadScale, outlineId = 254, spriteOffsetNorth = -0.2f, sprites = SpriteSet("len"),
                },
            };
            var vertexColor = AssetDatabase.LoadAssetAtPath<Material>(PocSceneBuilder.ToonMaterialPath);
            if (vertexColor == null) throw new InvalidOperationException("ver4.0: the vertex colour material is missing");
            foreach (var id in NpcIds)
                looks.Add(new PocCharacterLook
                {
                    id = id, model = Load(Models + "npc_" + id + ".fbx"), material = vertexColor, scale = 1f, outlineId = 255,
                    spriteOffsetNorth = -0.22f, sprites = SpriteSet(id),
                });
            return looks.ToArray();
        }

        private static GameObject Load(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) throw new InvalidOperationException("ver4.0 model missing: " + path);
            return model;
        }

        // The texture-tint material for Rin or Len (made once, kept as an asset so the shader ships).
        public static Material TintMaterial(string path, string texturePath)
        {
            var shader = Shader.Find(TextureTintShader);
            if (shader == null) throw new InvalidOperationException("ver4.0 shader missing: " + TextureTintShader);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null) throw new InvalidOperationException("ver4.0 texture missing: " + texturePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_Bands", 3f);
            material.SetFloat("_ShadowFloor", 0.35f);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static PocSpriteSet SpriteSet(string id)
        {
            Sprite[] Frames(string dir, bool walk) => walk
                ? new[] { Sprite(id, dir + "_walk_0"), Sprite(id, dir + "_walk_1") }
                : new[] { Sprite(id, dir + "_idle_0") };
            return new PocSpriteSet
            {
                frontIdle = Frames("front", false), backIdle = Frames("back", false), leftIdle = Frames("left", false), rightIdle = Frames("right", false),
                frontWalk = Frames("front", true), backWalk = Frames("back", true), leftWalk = Frames("left", true), rightWalk = Frames("right", true),
                offsetY = 0f,
            };
        }

        private static Sprite Sprite(string id, string name)
        {
            var path = Sprites + id + "/" + name + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("ver4.0 sprite missing: " + path);
            return sprite;
        }
    }

    // ver4.0: the characters' 2D sprites (made by tools/poc2d3d/v40_sprites.py): pixel art at the 2D
    // game's density (64 pixels per unit), Point filtering, no compression, the feet at the bottom centre.
    public sealed class PocVer40SpritePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(PocSceneBuilderVer40.Sprites, StringComparison.Ordinal))
            {
                var importer = (TextureImporter)assetImporter;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spritePivot = new Vector2(0.5f, 0f);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                return;
            }
            if (assetPath.StartsWith(PocSceneBuilderVer40.Models, StringComparison.Ordinal) && assetPath.EndsWith("_tex.png", StringComparison.Ordinal))
            {
                // Rin's and Len's texture (their own rin.png at 512 x 512).
                var importer = (TextureImporter)assetImporter;
                importer.textureType = TextureImporterType.Default;
                importer.maxTextureSize = 512;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Repeat;
            }
        }
    }
}
