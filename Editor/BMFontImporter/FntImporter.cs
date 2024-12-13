using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using TMPro;

[assembly: InternalsVisibleTo("Unity.TextMeshPro")]
namespace Mx
{

    public class BFImporter : AssetPostprocessor
    {

        #region  helper
        public struct Kerning
        {
            public int first;
            public int second;
            public int amount;
        }

        public class FntParse
        {
            public int textureWidth;
            public int textureHeight;
            public string[] textureNames;

            public string fontName;
            public int fontSize;
            public int lineHeight;
            public int lineBaseHeight;


            public List<TMP_Character> chars = new();
            public Dictionary<uint, TMP_Character> lut = new();
            public List<Glyph> glyphs = new();

            public CharacterInfo[] charInfos { get; private set; }
            public Kerning[] kernings { get; private set; }

            public static FntParse GetFntParse(ref string text)
            {
                FntParse parse = null;
                if (text.StartsWith("info"))
                {
                    parse = new FntParse();
                    parse.DoTextParse(ref text);
                }
                else if (text.StartsWith("<"))
                {
                    parse = new FntParse();
                    parse.DoXMLPase(ref text);
                }
                return parse;
            }

            #region xml
            public void DoXMLPase(ref string content)
            {
                XmlDocument xml = new();
                xml.LoadXml(content);
                XmlElement rootNode = xml.DocumentElement;

                XmlNode info = rootNode.SelectSingleNode("info");
                XmlNode common = rootNode.SelectSingleNode("common");
                XmlNodeList pages = rootNode.SelectNodes("pages/page");
                XmlNodeList chars = rootNode.SelectNodes("chars/char");


                fontName = info.Attributes.GetNamedItem("face").InnerText;
                fontSize = ToInt(info, "size");

                lineHeight = ToInt(common, "lineHeight");
                lineBaseHeight = ToInt(common, "base");
                textureWidth = ToInt(common, "scaleW");
                textureHeight = ToInt(common, "scaleH");
                int pageNum = ToInt(common, "pages");
                textureNames = new string[pageNum];

                for (int i = 0; i < pageNum; i++)
                {
                    XmlNode page = pages[i];
                    int pageId = ToInt(page, "id");
                    textureNames[pageId] = page.Attributes.GetNamedItem("file").InnerText;
                }

                charInfos = new CharacterInfo[chars.Count];
                for (int i = 0; i < chars.Count; i++)
                {
                    XmlNode charNode = chars[i];
                    var id = ToInt(charNode, "id");
                    var glyph = CreateCharGlyph(
                        (uint)glyphs.Count,
                        ToInt(charNode, "x"),
                        ToInt(charNode, "y"),
                        ToInt(charNode, "width"),
                        ToInt(charNode, "height"),
                        ToInt(charNode, "xoffset"),
                        ToInt(charNode, "yoffset"),
                        ToInt(charNode, "xadvance"),
                        ToInt(charNode, "page")
                    );
                    var chara = new TMP_Character((uint)id, glyph);
                    glyphs.Add(glyph);
                    lut.Add((uint)id, chara);
                    this.chars.Add(chara);
                }

                // kernings
                XmlNodeList kerns = rootNode.SelectNodes("kernings/kerning");
                if (kerns != null && kerns.Count > 0)
                {
                    kernings = new Kerning[kerns.Count];
                    for (int i = 0; i < kerns.Count; i++)
                    {
                        XmlNode kerningNode = kerns[i];
                        kernings[i] = new Kerning
                        {
                            first = ToInt(kerningNode, "first"),
                            second = ToInt(kerningNode, "second"),
                            amount = ToInt(kerningNode, "amount")
                        };
                    }
                }
            }


            private static int ToInt(XmlNode node, string name)
            {
                return int.Parse(node.Attributes.GetNamedItem(name).InnerText);
            }
            #endregion

            #region text
            private Regex pattern;
            public void DoTextParse(ref string content)
            {
                // letter=\" \"     // \S+=\\?".+?\\?"
                // letter=" "       // \S+=\\?".+?\\?"
                // letter="x"       // \S+=\\?".+?\\?"
                // letter="""       // \S+=\\?".+?\\?"
                // letter=""        // \S+
                // char             // \S+
                pattern = new Regex(@"\S+=\\?"".+?\\?""|\S+");
                string[] lines = content.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                ReadTextInfo(ref lines[0]);
                ReadTextCommon(ref lines[1]);

                for (int j = 0; j < textureNames.Length; j++)
                {
                    ReadTextPage(ref lines[j + 2]);
                }

                // don't use count of chars, count is incorrect if has space 
                //ReadTextCharCount(ref lines[3]);
                List<CharacterInfo> list = new();
                int i = 2 + textureNames.Length;
                int l = lines.Length;
                for (; i < l; i++)
                {
                    if (!ReadTextChar(i - 4, ref lines[i], ref list))
                        break;
                }
                charInfos = list.ToArray();

                // skip empty line
                for (; i < l; i++)
                {
                    if (lines[i].Length > 0)
                        break;
                }

                // kernings
                if (i < l)
                {
                    if (ReadTextCount(ref lines[i++], out int count))
                    {
                        int start = i;
                        kernings = new Kerning[count];
                        for (; i < l; i++)
                        {
                            if (!ReadTextKerning(i - start, ref lines[i], ref list))
                                break;
                        }
                    };
                }
            }

            private void ReadTextInfo(ref string line)
            {
                SplitParts(line, out string[] keys, out string[] values);
                for (int i = keys.Length - 1; i >= 0; i--)
                {
                    switch (keys[i])
                    {
                        case "face": fontName = values[i]; break;
                        case "size": fontSize = int.Parse(values[i]); break;
                    }
                }
            }

            private void ReadTextCommon(ref string line)
            {
                SplitParts(line, out string[] keys, out string[] values);
                for (int i = keys.Length - 1; i >= 0; i--)
                {
                    switch (keys[i])
                    {
                        case "lineHeight": lineHeight = int.Parse(values[i]); break;
                        case "base": lineBaseHeight = int.Parse(values[i]); break;
                        case "scaleW": textureWidth = int.Parse(values[i]); break;
                        case "scaleH": textureHeight = int.Parse(values[i]); break;
                        case "pages": textureNames = new string[int.Parse(values[i])]; break;
                    }
                }
            }

            private void ReadTextPage(ref string line)
            {
                SplitParts(line, out string[] keys, out string[] values);
                string textureName = null;
                int pageId = -1;
                for (int i = keys.Length - 1; i >= 0; i--)
                {
                    switch (keys[i])
                    {
                        case "file": textureName = values[i]; break;
                        case "id": pageId = int.Parse(values[i]); break;
                    }
                }
                textureNames[pageId] = textureName;
            }

            private bool ReadTextCount(ref string line, out int count)
            {
                SplitParts(line, out string[] keys, out string[] values);
                count = 0;
                for (int i = keys.Length - 1; i >= 0; i--)
                {
                    switch (keys[i])
                    {
                        case "count":
                            count = int.Parse(values[i]);
                            return true;
                    }
                }
                return false;
            }

            private bool ReadTextChar(int idx, ref string line, ref List<CharacterInfo> list)
            {
                if (!line.StartsWith("char")) return false;
                SplitParts(line, out string[] keys, out string[] values);
                int id = 0, x = 0, y = 0, w = 0, h = 0;
                int xo = 0, yo = 0, xadvance = 0, page = 0;
                for (int i = keys.Length - 1; i >= 0; i--)
                {
                    switch (keys[i])
                    {
                        case "id": id = int.Parse(values[i]); break;
                        case "x": x = int.Parse(values[i]); break;
                        case "y": y = int.Parse(values[i]); break;
                        case "width": w = int.Parse(values[i]); break;
                        case "height": h = int.Parse(values[i]); break;
                        case "xoffset": xo = int.Parse(values[i]); break;
                        case "yoffset": yo = int.Parse(values[i]); break;
                        case "xadvance": xadvance = int.Parse(values[i]); break;
                        case "page": page = int.Parse(values[i]); break;
                    }
                }
                var glyph = CreateCharGlyph((uint)glyphs.Count, x, y, w, h, xo, yo, xadvance);
                var chara = new TMP_Character((uint)id, glyph);
                glyphs.Add(glyph);
                chars.Add(chara);
                lut.TryAdd((uint)id, chara);
                return true;
            }

            private bool ReadTextKerning(int idx, ref string line, ref List<CharacterInfo> list)
            {
                if (!line.StartsWith("kerning")) return false;
                SplitParts(line, out string[] keys, out string[] values);
                Kerning kerning = new();
                for (int i = keys.Length - 1; i >= 0; i--)
                {
                    switch (keys[i])
                    {
                        case "first": kerning.first = int.Parse(values[i]); break;
                        case "second": kerning.second = int.Parse(values[i]); break;
                        case "amount": kerning.amount = int.Parse(values[i]); break;
                    }
                }
                kernings[idx] = kerning;
                return true;
            }

            private bool SplitParts(string line, out string[] keys, out string[] values)
            {
                MatchCollection parts = pattern.Matches(line);
                int count = parts.Count;
                keys = new string[count - 1];
                values = new string[count - 1];
                for (int i = count - 2; i >= 0; i--)
                {
                    string part = parts[i + 1].Value;
                    int pos = part.IndexOf('=');
                    keys[i] = part.Substring(0, pos);
                    values[i] = part.Substring(pos + 1).Trim('"');
                }
                return true;
            }

            #endregion



            private Glyph CreateCharGlyph(uint idx, int x, int y, int w, int h, int xo, int yo, int xadvance, int page = 0)
            {

                Rect vert = new()
                {
                    x = x,
                    y = textureHeight - y - h,
                    width = w,
                    height = h
                };
                //vert.height = -vert.height;


                var rect = new GlyphRect(vert);
                var mtx = new GlyphMetrics(vert.width, vert.height, xo, yo, xadvance);

                var g = new Glyph(idx, mtx, rect);

                return g;
            }

        }

        #endregion

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string str in importedAssets)
            {
                DoImportBitmapFont(str);
            }
        }

        public static bool IsFnt(string path)
        {
            return path.EndsWith(".fnt", StringComparison.OrdinalIgnoreCase);
        }

        public static void DoImportBitmapFont(string fntPatn)
        {
            if (!IsFnt(fntPatn)) return;

            TextAsset fnt = AssetDatabase.LoadMainAssetAtPath(fntPatn) as TextAsset;
            string text = fnt.text;


            string fntName = Path.GetFileNameWithoutExtension(fntPatn);
            string rootPath = Path.GetDirectoryName(fntPatn);
            string fontPath = string.Format("{0}/{1}.asset", rootPath, fntName);
            FntParse parse = FntParse.GetFntParse(ref text);

            Texture2D textures = DoImportTextures(parse, rootPath, fnt);


            TMP_FontAsset font = AssetDatabase.LoadMainAssetAtPath(fontPath) as TMP_FontAsset;
            if (font == null)
            {
                font = ScriptableObject.CreateInstance<TMP_FontAsset>();
                AssetDatabase.CreateAsset(font, fontPath);
                AssetDatabase.WriteImportSettingsIfDirty(fontPath);
            }

            font.version = "1.1.0";

            font.glyphTable = parse.glyphs;
            font.characterTable = parse.chars;
            font.m_CharacterLookupDictionary = parse.lut;

            font.m_CharacterLookupDictionary[0x2026] = new(0x2026, font.m_CharacterLookupDictionary[0].glyph);
            font.m_CharacterLookupDictionary[0x5F] = new(0x5F, font.m_CharacterLookupDictionary[0].glyph);
            font.characterTable.Add(font.m_CharacterLookupDictionary[0x2026]);
            font.characterTable.Add(font.m_CharacterLookupDictionary[0x5F]);

            font.atlas = textures;
            font.atlasTextures = new[] { textures };
            font.atlasWidth = textures.width;
            font.atlasHeight = textures.height;
            font.atlasPadding = 0;
            font.atlasRenderMode = GlyphRenderMode.COLOR;

            var faceInfo = new FaceInfo
            {
                baseline = parse.lineBaseHeight,
                lineHeight = parse.lineHeight,
                ascentLine = Math.Abs(parse.fontSize),
                pointSize = Math.Abs(parse.fontSize),
            };

            font.faceInfo = faceInfo;


            Material material = AssetDatabase.LoadAssetAtPath(fontPath, typeof(Material)) as Material;
            if (font.material == null)
            {
                material = new Material(Shader.Find("TextMeshPro/Bitmap Custom Atlas Opt"))
                {
                    name = fntName + " Material"
                };
                AssetDatabase.AddObjectToAsset(material, fontPath);
            }
            else
                material.shader = Shader.Find("TextMeshPro/Bitmap Custom Atlas Opt");


            font.InitializeDictionaryLookupTables();


            material.name = fntName + " Material";

            material.SetTexture(ShaderUtilities.ID_MainTex, textures);
            material.SetFloat(ShaderUtilities.ID_TextureWidth, textures.width);
            material.SetFloat(ShaderUtilities.ID_TextureHeight, textures.height);


            material.SetFloat(ShaderUtilities.ID_GradientScale, 0 + 1);

            material.SetFloat(ShaderUtilities.ID_WeightNormal, font.normalStyle);
            material.SetFloat(ShaderUtilities.ID_WeightBold, font.boldStyle);
            EditorUtility.SetDirty(material);

            font.material = material;
            font.ReadFontAssetDefinition();
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
        }

        private static Texture2D DoImportTextures(FntParse parse, string rootPath, TextAsset fnt)
        {
            // The texture name of the file generated by ShoeBox uses an absolute path
            string textureName = Path.GetFileName(parse.textureNames[0]);
            string texPath = string.Format("{0}/{1}", rootPath, textureName);

            Texture2D texture = AssetDatabase.LoadMainAssetAtPath(texPath) as Texture2D;
            if (texture == null)
            {
                Debug.LogErrorFormat(fnt, "{0}: not found '{1}'.", typeof(BFImporter), texPath);
            }

            TextureImporter texImporter = AssetImporter.GetAtPath(texPath) as TextureImporter;
            texImporter.textureType = TextureImporterType.Sprite;
            texImporter.mipmapEnabled = false;
            texImporter.wrapMode = TextureWrapMode.Clamp;
            texImporter.alphaIsTransparency = true;
            texImporter.alphaSource = TextureImporterAlphaSource.FromInput;

            texImporter.streamingMipmaps = false;

            // Sprite
            texImporter.spriteImportMode = SpriteImportMode.Single;

            texImporter.SaveAndReimport();
            return texture;
        }
    }
}
