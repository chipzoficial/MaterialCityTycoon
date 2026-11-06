using System.IO;
using System.Linq;
using System.Text;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using NUnit.Framework;
using UnityEditor;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class TextEncodingRegressionTests
    {
        private static readonly string[] UserFacingTextSources =
        {
            "Assets/_Game/Scripts/Building/BuildHud.cs",
            "Assets/_Game/Editor/ConfigureBuildHudMenu.cs",
            "Assets/_Game/Editor/ConfigureDisplayFurnitureMenu.cs",
            "Assets/_Game/Editor/ConfigureProductCatalogMenu.cs",
            "Assets/_Game/Editor/RepairPortugueseTextEncodingMenu.cs"
        };

        private static readonly byte[][] MojibakeUtf8Sequences =
        {
            new byte[] { 0xc3, 0x83 }, // U+00C3: the first character of Ã.
            new byte[] { 0xc3, 0x82 }, // U+00C2: the first character of Â.
            new byte[] { 0xc3, 0xa2, 0xe2, 0x82, 0xac }, // The start of â€.
            new byte[] { 0xef, 0xbf, 0xbd } // U+FFFD replacement character.
        };

        [Test]
        public void UserFacingTextSourcesAreStrictUtf8AndDoNotContainKnownMojibakeMarkers()
        {
            UTF8Encoding strictUtf8 = new(false, true);
            foreach (string assetPath in UserFacingTextSources)
            {
                string absolutePath = Path.GetFullPath(assetPath);
                byte[] bytes = File.ReadAllBytes(absolutePath);
                strictUtf8.GetString(bytes);
                foreach (byte[] sequence in MojibakeUtf8Sequences)
                {
                    Assert.That(ContainsSequence(bytes, sequence), Is.False,
                        $"Unexpected mojibake UTF-8 sequence in {assetPath}.");
                }
            }
        }

        [Test]
        public void GeneratedProductAndFurnitureNamesPreservePortugueseAccents()
        {
            ProductDefinition paint = Load<ProductDefinition>("Assets/_Game/ScriptableObjects/Products/DEF_TintaAcrilica18L.asset");
            ProductDefinition wallPlugs = Load<ProductDefinition>("Assets/_Game/ScriptableObjects/Products/DEF_BuchasPlasticasN8.asset");
            FurnitureDefinition gondola = Load<FurnitureDefinition>("Assets/_Game/ScriptableObjects/Furniture/DEF_DoubleSidedGondola.asset");

            Assert.That(paint.DisplayName, Is.EqualTo("Tinta acrílica 18 L"));
            Assert.That(wallPlugs.DisplayName, Is.EqualTo("Buchas plásticas nº 8"));
            Assert.That(gondola.DisplayName, Is.EqualTo("Gôndola Central"));
            Assert.That(gondola.DisplaySlots.Select(slot => slot.DisplayName), Is.EquivalentTo(new[]
            {
                "Frente — nível inferior",
                "Frente — nível superior",
                "Verso — nível inferior",
                "Verso — nível superior"
            }));
        }

        private static T Load<T>(string assetPath) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            Assert.That(asset, Is.Not.Null, assetPath);
            return asset;
        }

        private static bool ContainsSequence(byte[] bytes, byte[] sequence)
        {
            for (int offset = 0; offset <= bytes.Length - sequence.Length; offset++)
            {
                bool matches = true;
                for (int index = 0; index < sequence.Length; index++)
                {
                    if (bytes[offset + index] == sequence[index]) continue;
                    matches = false;
                    break;
                }

                if (matches) return true;
            }

            return false;
        }
    }
}
