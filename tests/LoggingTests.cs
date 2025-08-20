using GdUnit4;
using static GdUnit4.Assertions;
using DMapImporter.Core.Utility;
using System;
using System.IO;

namespace DMapImporter.Tests
{
    [TestSuite]
    public class LoggingTests
    {
        [TestCase]
        public void TestLogLevelsExist()
        {
            // Test that all log levels are available
            AssertThat(Log.LogLevel.Debug).IsEqual(Log.LogLevel.Debug);
            AssertThat(Log.LogLevel.Info).IsEqual(Log.LogLevel.Info);
            AssertThat(Log.LogLevel.Warn).IsEqual(Log.LogLevel.Warn);
            AssertThat(Log.LogLevel.Error).IsEqual(Log.LogLevel.Error);
        }
        
        [TestCase]
        public void TestLogLevelOrdering()
        {
            // Test that log levels have correct numeric ordering
            AssertThat((int)Log.LogLevel.Debug).IsLess((int)Log.LogLevel.Info);
            AssertThat((int)Log.LogLevel.Info).IsLess((int)Log.LogLevel.Warn);
            AssertThat((int)Log.LogLevel.Warn).IsLess((int)Log.LogLevel.Error);
        }
        
        [TestCase]
        public void TestLogLevelFiltering()
        {
            // Test that minimum log level filtering works
            var originalLevel = Log.MinimumLogLevel;
            
            try
            {
                // Set to Warn level - should filter out Debug and Info
                Log.SetMinimumLogLevel(Log.LogLevel.Warn);
                AssertThat(Log.MinimumLogLevel).IsEqual(Log.LogLevel.Warn);
                
                // Test convenience methods
                Log.EnableDebugLogging();
                AssertThat(Log.MinimumLogLevel).IsEqual(Log.LogLevel.Debug);
                
                Log.SetQuietMode();
                AssertThat(Log.MinimumLogLevel).IsEqual(Log.LogLevel.Error);
            }
            finally
            {
                // Restore original level
                Log.SetMinimumLogLevel(originalLevel);
            }
        }
        
        [TestCase]
        public void TestLoggingMethodsExist()
        {
            // Test that all logging methods exist and can be called without exceptions
            try
            {
                Log.Debug("Test debug message");
                Log.Info("Test info message");
                Log.Warn("Test warning message");
                Log.Error("Test error message");
                
                // Test error with exception
                var testException = new InvalidOperationException("Test exception");
                Log.Error("Test error with exception", testException);
            }
            catch (Exception ex)
            {
                AssertThat(false).OverrideFailureMessage($"Logging methods should not throw exceptions: {ex.Message}").IsTrue();
            }
        }
        
        [TestCase]
        public void TestEnvironmentInfo()
        {
            // Test that environment info can be retrieved
            var envInfo = Log.GetEnvironmentInfo();
            AssertThat(envInfo).IsNotNull();
            AssertThat(envInfo).Contains("Logging Environment:");
            AssertThat(envInfo).Contains("Min Level:");
            
            // Should contain either "Godot Engine" or "Standalone Console"
            bool hasValidEnvironment = envInfo.Contains("Godot Engine") || envInfo.Contains("Standalone Console");
            AssertThat(hasValidEnvironment).IsTrue();
        }
        
        [TestCase]
        public void TestBackwardsCompatibility()
        {
            // Test that legacy method signatures still work (even if deprecated)
            try
            {
                #pragma warning disable CS0618 // Type or member is obsolete
                Log.Error("Test backwards compatibility error", ConsoleColor.Red, ConsoleColor.White);
                Log.Warn("Test backwards compatibility warn", ConsoleColor.Yellow, ConsoleColor.Black);
                #pragma warning restore CS0618 // Type or member is obsolete
            }
            catch (Exception ex)
            {
                AssertThat(false).OverrideFailureMessage($"Legacy methods should still work: {ex.Message}").IsTrue();
            }
        }
        
        [TestCase]
        public void TestLogLevelConfiguration()
        {
            var originalLevel = Log.MinimumLogLevel;
            
            try
            {
                // Test setting different log levels
                foreach (Log.LogLevel level in Enum.GetValues<Log.LogLevel>())
                {
                    Log.SetMinimumLogLevel(level);
                    AssertThat(Log.MinimumLogLevel).IsEqual(level);
                }
            }
            finally
            {
                Log.SetMinimumLogLevel(originalLevel);
            }
        }
        
        [TestCase]
        public void TestGodotDetectionStability()
        {
            // Test that Godot detection doesn't crash and is consistent
            var envInfo1 = Log.GetEnvironmentInfo();
            var envInfo2 = Log.GetEnvironmentInfo();
            
            // Environment detection should be consistent between calls
            AssertThat(envInfo1).IsEqual(envInfo2);
            
            // Test that logging works multiple times without issues
            for (int i = 0; i < 5; i++)
            {
                Log.Debug($"Stability test {i}");
                Log.Info($"Stability test {i}");
            }
        }
    }
}