using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DuelGenesis.Cards
{
    public static class ProductionCardArtRegistry
    {
        private const string FacesFolder = "DMOCardFaces";
        private const string FramesFolder = "DMOCardFrames";

        private static readonly Dictionary<string, Texture2D> FaceCache = new();
        private static readonly Dictionary<string, string> FacePathIndex = new();
        private static readonly Dictionary<string, Texture2D> FrameCache = new();
        private static bool _faceIndexBuilt;

        public static Texture2D LoadDisplayTexture(CardData card)
        {
            if (card == null) return null;
            return LoadFace(card.cardName) ?? LoadFrame(card.ResolvedFrameKind);
        }

        public static Texture2D LoadFace(string cardName)
        {
            string key = Normalize(cardName);
            if (string.IsNullOrEmpty(key)) return null;

            if (FaceCache.TryGetValue(key, out Texture2D cached))
                return cached;

            EnsureFaceIndex();
            if (!FacePathIndex.TryGetValue(key, out string path) || !File.Exists(path))
                return null;

            Texture2D texture = LoadPng(path, cardName);
            if (texture != null)
                FaceCache[key] = texture;
            return texture;
        }

        public static Texture2D LoadCardBack()
        {
            return LoadFrameFile("Card Back Anime 1.png", "CARD_BACK");
        }

        public static Texture2D LoadFrame(CardFrameKind frameKind)
        {
            string file = FrameFileName(frameKind);
            return string.IsNullOrEmpty(file) ? null : LoadFrameFile(file, frameKind.ToString());
        }

        public static string FrameFileName(CardFrameKind frameKind)
        {
            return frameKind switch
            {
                CardFrameKind.NormalMonster => "Normal Monster Card Background.png",
                CardFrameKind.EffectMonster => "Effect Monster Card Background.png",
                CardFrameKind.FusionMonster => "Fusion Card Background Texture.png",
                CardFrameKind.RitualMonster => "Ritual Card Background Texture.png",
                CardFrameKind.SynchroMonster => "Synchro Background Texture.png",
                CardFrameKind.XyzMonster => "XYZ Card Background.png",
                CardFrameKind.Spell => "Spell Card Background Texture.png",
                CardFrameKind.Trap => "Trap Card Background Texture.png",
                CardFrameKind.Token => "Token Background Texture.png",
                _ => "Card Background Frame.png"
            };
        }

        public static bool HasAuthoritativeFace(CardData card)
        {
            if (card == null) return false;
            EnsureFaceIndex();
            return FacePathIndex.ContainsKey(Normalize(card.cardName));
        }

        public static void ClearCaches()
        {
            FaceCache.Clear();
            FrameCache.Clear();
            FacePathIndex.Clear();
            _faceIndexBuilt = false;
        }

        private static void EnsureFaceIndex()
        {
            if (_faceIndexBuilt) return;
            _faceIndexBuilt = true;

            string folder = ResolveFacesFolder();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            foreach (string path in Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
            {
                string key = Normalize(Path.GetFileNameWithoutExtension(path));
                if (!string.IsNullOrEmpty(key) && !FacePathIndex.ContainsKey(key))
                    FacePathIndex.Add(key, path);
            }
        }

        private static Texture2D LoadFrameFile(string fileName, string cacheKey)
        {
            if (FrameCache.TryGetValue(cacheKey, out Texture2D cached))
                return cached;

            string folder = ResolveFramesFolder();
            if (string.IsNullOrWhiteSpace(folder)) return null;

            string path = Path.Combine(folder, fileName);
            if (!File.Exists(path)) return null;

            Texture2D texture = LoadPng(path, cacheKey);
            if (texture != null)
                FrameCache[cacheKey] = texture;
            return texture;
        }

        private static Texture2D LoadPng(string path, string displayName)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "DMO Card - " + displayName,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                return texture;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not load production card image '{path}': {exception.Message}");
                return null;
            }
        }

        private static string ResolveFacesFolder()
        {
#if UNITY_EDITOR
            string editorSource = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Cards", "DMO_card_art", "cards"));
            if (Directory.Exists(editorSource)) return editorSource;
#endif
            string builtSource = Path.Combine(Application.streamingAssetsPath, FacesFolder);
            return Directory.Exists(builtSource) ? builtSource : null;
        }

        private static string ResolveFramesFolder()
        {
#if UNITY_EDITOR
            string editorSource = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Cards", "DMO_card_art", "card_backs_frames"));
            if (Directory.Exists(editorSource)) return editorSource;
#endif
            string builtSource = Path.Combine(Application.streamingAssetsPath, FramesFolder);
            return Directory.Exists(builtSource) ? builtSource : null;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c))
                    builder.Append(char.ToLowerInvariant(c));
            }
            return builder.ToString();
        }
    }
}
