using GdUnit4;
using static GdUnit4.Assertions;
using DMapImporter.Core.Dmap;
using DMapImporter.Core.Utility;

namespace DMapImporter.Tests
{
    [TestSuite]
    public class SoundTests
    {
        [TestCase]
        public void DefaultConstructor_InitializesWithDefaultValues()
        {
            var sound = new Sound();

            AssertThat(sound.SoundFile).IsNull();
            AssertThat(sound.Position.X).IsEqual(0);
            AssertThat(sound.Position.Y).IsEqual(0);
            AssertThat(sound.Volume).IsEqual(0u);
            AssertThat(sound.Range).IsEqual(0u);
        }

        [TestCase]
        public void Constructor_WithInitializer_SetsProperties()
        {
            var position = new PixelPosition { X = 300, Y = 450 };
            var sound = new Sound
            {
                SoundFile = "ambient_forest.wav",
                Position = position,
                Volume = 75u,
                Range = 200u
            };

            AssertThat(sound.SoundFile).IsEqual("ambient_forest.wav");
            AssertThat(sound.Position.X).IsEqual(300);
            AssertThat(sound.Position.Y).IsEqual(450);
            AssertThat(sound.Volume).IsEqual(75u);
            AssertThat(sound.Range).IsEqual(200u);
        }

        [TestCase]
        [DataPoint(nameof(SoundFiles))]
        public void SoundFile_AcceptsVariousValues(string soundFile)
        {
            var sound = new Sound { SoundFile = soundFile };

            AssertThat(sound.SoundFile).IsEqual(soundFile);
        }

        [TestCase]
        [DataPoint(nameof(PositionValues))]
        public void Position_AcceptsVariousValues(int x, int y)
        {
            var position = new PixelPosition { X = x, Y = y };
            var sound = new Sound { Position = position };

            AssertThat(sound.Position.X).IsEqual(x);
            AssertThat(sound.Position.Y).IsEqual(y);
        }

        [TestCase]
        [DataPoint(nameof(VolumeValues))]
        public void Volume_AcceptsVariousValues(uint volume)
        {
            var sound = new Sound { Volume = volume };

            AssertThat(sound.Volume).IsEqual(volume);
        }

        [TestCase]
        [DataPoint(nameof(RangeValues))]
        public void Range_AcceptsVariousValues(uint range)
        {
            var sound = new Sound { Range = range };

            AssertThat(sound.Range).IsEqual(range);
        }

        [TestCase]
        public void Struct_IsImmutableAfterCreation()
        {
            var sound = new Sound
            {
                SoundFile = "battle_music.mp3",
                Position = new PixelPosition { X = 500, Y = 600 },
                Volume = 100u,
                Range = 1000u
            };

            AssertThat(sound.SoundFile).IsEqual("battle_music.mp3");
            AssertThat(sound.Position.X).IsEqual(500);
            AssertThat(sound.Position.Y).IsEqual(600);
            AssertThat(sound.Volume).IsEqual(100u);
            AssertThat(sound.Range).IsEqual(1000u);
        }

        [TestCase]
        public void TwoSounds_WithSameValues_AreEqual()
        {
            var position = new PixelPosition { X = 150, Y = 250 };
            var sound1 = new Sound
            {
                SoundFile = "footsteps.wav",
                Position = position,
                Volume = 50u,
                Range = 100u
            };
            var sound2 = new Sound
            {
                SoundFile = "footsteps.wav",
                Position = position,
                Volume = 50u,
                Range = 100u
            };

            AssertThat(sound1.SoundFile).IsEqual(sound2.SoundFile);
            AssertThat(sound1.Position.X).IsEqual(sound2.Position.X);
            AssertThat(sound1.Position.Y).IsEqual(sound2.Position.Y);
            AssertThat(sound1.Volume).IsEqual(sound2.Volume);
            AssertThat(sound1.Range).IsEqual(sound2.Range);
        }

        [TestCase]
        public void Sound_WithNullFile_HandledCorrectly()
        {
            var sound = new Sound
            {
                SoundFile = null!,
                Position = new PixelPosition { X = 0, Y = 0 },
                Volume = 0u,
                Range = 0u
            };

            AssertThat(sound.SoundFile).IsNull();
            AssertThat(sound.Position.X).IsEqual(0);
            AssertThat(sound.Position.Y).IsEqual(0);
            AssertThat(sound.Volume).IsEqual(0u);
            AssertThat(sound.Range).IsEqual(0u);
        }

        [TestCase]
        public void Sound_WithMaxValues_HandledCorrectly()
        {
            var sound = new Sound
            {
                SoundFile = "max_volume_sound.wav",
                Position = new PixelPosition { X = int.MaxValue, Y = int.MinValue },
                Volume = uint.MaxValue,
                Range = uint.MaxValue
            };

            AssertThat(sound.SoundFile).IsEqual("max_volume_sound.wav");
            AssertThat(sound.Position.X).IsEqual(int.MaxValue);
            AssertThat(sound.Position.Y).IsEqual(int.MinValue);
            AssertThat(sound.Volume).IsEqual(uint.MaxValue);
            AssertThat(sound.Range).IsEqual(uint.MaxValue);
        }

        public static object[][] SoundFiles => new object[][]
        {
            new object[] { "ambient_forest.wav" },
            new object[] { "battle_music.mp3" },
            new object[] { "footsteps.wav" },
            new object[] { "explosion.ogg" },
            new object[] { "magic_spell.wav" },
            new object[] { "" },
            new object[] { "very_long_sound_file_name_that_might_be_used.wav" },
            new object[] { "123_numeric_sound.mp3" },
            new object[] { "special!@#$%^&*()_sound.wav" }
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

        public static object[][] VolumeValues => new object[][]
        {
            new object[] { 0u },
            new object[] { 1u },
            new object[] { 50u },
            new object[] { 100u },
            new object[] { 255u },
            new object[] { uint.MaxValue }
        };

        public static object[][] RangeValues => new object[][]
        {
            new object[] { 0u },
            new object[] { 1u },
            new object[] { 100u },
            new object[] { 500u },
            new object[] { 1000u },
            new object[] { uint.MaxValue }
        };
    }
}