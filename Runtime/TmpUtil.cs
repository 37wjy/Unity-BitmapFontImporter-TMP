using TMPro;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine.TextCore.Text;

using MaterialReferenceManager = TMPro.MaterialReferenceManager;

namespace Mx
{
    public class TmpUtil
    {
        public static void AddMaterial(Material mat)
        {
            var hash = TMP_TextUtilities.GetSimpleHashCode(mat.name);
    #if DEBUG
            if (MaterialReferenceManager.TryGetMaterial(hash, out _))
            {
                throw new System.Exception($"Font Material Already Add {mat.name}");
            }
    #endif
            MaterialReferenceManager.AddFontMaterial(hash, mat);
        }

        public static void RemoveMaterial(Material mat)
        {
            var hash = TMP_TextUtilities.GetSimpleHashCode(mat.name);
            Dictionary<int, Material> m_FontMaterialReferenceLookup;

            var fi = typeof(MaterialReferenceManager).GetField("m_FontMaterialReferenceLookup", BindingFlags.Instance | BindingFlags.NonPublic);
            m_FontMaterialReferenceLookup = fi.GetValue(MaterialReferenceManager.instance) as Dictionary<int, Material>;
            m_FontMaterialReferenceLookup.Remove(hash);
        }

        public static void AddFont(TMP_FontAsset font)
        {
    #if DEBUG
            if (MaterialReferenceManager.TryGetFontAsset(font.hashCode, out _))
            {
                throw new System.Exception($"Font Already Add {font.name}");
            }
    #endif
            MaterialReferenceManager.AddFontAsset(font);
        }

        public static void RemoveFont(TMP_FontAsset font)
        {
            var hash = font.hashCode;
            Dictionary<int, TMP_FontAsset> m_FontAssetReferenceLookup;

            var fi = typeof(MaterialReferenceManager).GetField("m_FontAssetReferenceLookup", BindingFlags.Instance | BindingFlags.NonPublic);
            m_FontAssetReferenceLookup = fi.GetValue(MaterialReferenceManager.instance) as Dictionary<int, TMP_FontAsset>;
            m_FontAssetReferenceLookup.Remove(hash);

            if (font.material != null)
            {
                Dictionary<int, Material> m_FontMaterialReferenceLookup;

                fi = typeof(MaterialReferenceManager).GetField("m_FontMaterialReferenceLookup", BindingFlags.Instance | BindingFlags.NonPublic);
                m_FontMaterialReferenceLookup = fi.GetValue(MaterialReferenceManager.instance) as Dictionary<int, Material>;
                m_FontMaterialReferenceLookup.Remove(font.materialHashCode);
            }
        }

        public static void AddSprite(TMP_SpriteAsset sprite)
        {
            int hash = GetSpriteHashcode(sprite.name);
            sprite.hashCode = hash;
    #if DEBUG
            if (MaterialReferenceManager.TryGetSpriteAsset(hash, out _))
            {
                throw new System.Exception($"Sprite Asset Already Add {sprite.name}");
            }
    #endif
            MaterialReferenceManager.AddSpriteAsset(hash, sprite);
        }

        public static void RemoveSprite(TMP_SpriteAsset sprite)
        {
            int hash = GetSpriteHashcode(sprite.name);

            var fi = typeof(MaterialReferenceManager).GetField("m_SpriteAssetReferenceLookup", BindingFlags.Instance | BindingFlags.NonPublic);
            var m_SpriteAssetReferenceLookup = fi.GetValue(MaterialReferenceManager.instance) as Dictionary<int, TMP_SpriteAsset>;
            m_SpriteAssetReferenceLookup.Remove(hash);

            fi = typeof(MaterialReferenceManager).GetField("m_FontMaterialReferenceLookup", BindingFlags.Instance | BindingFlags.NonPublic);
            var m_FontMaterialReferenceLookup = fi.GetValue(MaterialReferenceManager.instance) as Dictionary<int, Material>;
            m_FontMaterialReferenceLookup.Remove(hash);
        }

        static int GetSpriteHashcode(string str)
        {
            int valueHashCode = 0;
            foreach (var s in str)
            {
                int unicode = s;
                valueHashCode = (valueHashCode << 5) + valueHashCode ^ unicode;
            }
            return valueHashCode;
        }


        public static void ClearNull()
        {
            var fields = new[] { "m_FontAssetReferenceLookup", "m_SpriteAssetReferenceLookup", "m_FontMaterialReferenceLookup" };

            foreach (var fieldName in fields)
            {
                var fi = typeof(MaterialReferenceManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                var lookup = fi.GetValue(MaterialReferenceManager.instance) as Dictionary<int, object>;

                if (lookup != null)
                {
                    lookup.Where(kvp => kvp.Value == null).ToList().ForEach(kvp => lookup.Remove(kvp.Key));
                }
            }
        } 
    }
}