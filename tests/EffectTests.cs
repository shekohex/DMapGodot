using GdUnit4;
using static GdUnit4.Assertions;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;

namespace DMapImporter.Tests
{
    [TestSuite]
    public class EffectTests
    {
        [TestCase]
        public void DefaultConstructor_InitializesWithDefaultValues()
        {
            var effect = new Effect();

            AssertThat(effect.EffectName).IsNull();
            AssertThat(effect.Position.X).IsEqual(0);
            AssertThat(effect.Position.Y).IsEqual(0);
        }

        [TestCase]
        public void Constructor_WithInitializer_SetsProperties()
        {
            var position = new PixelPosition { X = 150, Y = 250 };
            var effect = new Effect
            {
                EffectName = "explosion.fx",
                Position = position
            };

            AssertThat(effect.EffectName).IsEqual("explosion.fx");
            AssertThat(effect.Position.X).IsEqual(150);
            AssertThat(effect.Position.Y).IsEqual(250);
        }

        [TestCase]
        [DataPoint(nameof(EffectNames))]
        public void EffectName_AcceptsVariousValues(string effectName)
        {
            var effect = new Effect { EffectName = effectName };

            AssertThat(effect.EffectName).IsEqual(effectName);
        }

        [TestCase]
        [DataPoint(nameof(PositionValues))]
        public void Position_AcceptsVariousValues(int x, int y)
        {
            var position = new PixelPosition { X = x, Y = y };
            var effect = new Effect { Position = position };

            AssertThat(effect.Position.X).IsEqual(x);
            AssertThat(effect.Position.Y).IsEqual(y);
        }

        [TestCase]
        public void Struct_IsImmutableAfterCreation()
        {
            var effect = new Effect
            {
                EffectName = "fire.fx",
                Position = new PixelPosition { X = 100, Y = 200 }
            };

            AssertThat(effect.EffectName).IsEqual("fire.fx");
            AssertThat(effect.Position.X).IsEqual(100);
            AssertThat(effect.Position.Y).IsEqual(200);
        }

        [TestCase]
        public void TwoEffects_WithSameValues_AreEqual()
        {
            var position = new PixelPosition { X = 100, Y = 100 };
            var effect1 = new Effect
            {
                EffectName = "lightning.fx",
                Position = position
            };
            var effect2 = new Effect
            {
                EffectName = "lightning.fx",
                Position = position
            };

            AssertThat(effect1.EffectName).IsEqual(effect2.EffectName);
            AssertThat(effect1.Position.X).IsEqual(effect2.Position.X);
            AssertThat(effect1.Position.Y).IsEqual(effect2.Position.Y);
        }

        [TestCase]
        public void Effect_WithNullName_HandledCorrectly()
        {
            var effect = new Effect
            {
                EffectName = null!,
                Position = new PixelPosition { X = 50, Y = 75 }
            };

            AssertThat(effect.EffectName).IsNull();
            AssertThat(effect.Position.X).IsEqual(50);
            AssertThat(effect.Position.Y).IsEqual(75);
        }

        [TestCase]
        public void Effect_WithEmptyName_HandledCorrectly()
        {
            var effect = new Effect
            {
                EffectName = "",
                Position = new PixelPosition { X = 0, Y = 0 }
            };

            AssertThat(effect.EffectName).IsEqual("");
            AssertThat(effect.Position.X).IsEqual(0);
            AssertThat(effect.Position.Y).IsEqual(0);
        }

        public static object[][] EffectNames => new object[][]
        {
            new object[] { "explosion.fx" },
            new object[] { "fire.fx" },
            new object[] { "lightning.fx" },
            new object[] { "water_splash.fx" },
            new object[] { "magic_circle.fx" },
            new object[] { "" },
            new object[] { "very_long_effect_name_that_might_be_used_in_some_cases.fx" },
            new object[] { "123_numeric_effect.fx" },
            new object[] { "special!@#$%^&*()_effect.fx" }
        };

        public static object[][] PositionValues => new object[][]
        {
            new object[] { 0, 0 },
            new object[] { 100, 200 },
            new object[] { -50, -75 },
            new object[] { int.MaxValue, int.MinValue },
            new object[] { 1920, 1080 },
            new object[] { -1920, -1080 }
        };
    }
}