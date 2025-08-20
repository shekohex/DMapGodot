using GdUnit4;
using static GdUnit4.Assertions;
using DMapImporter.Core.Utility;

namespace DMapImporter.Tests
{
    [TestSuite]
    public class LoggingDemonstrationTest
    {
        [TestCase]
        public void TestLoggingSystemDemo()
        {
            // Demonstrate the comprehensive logging system
            var originalLevel = Log.MinimumLogLevel;
            
            try
            {
                // Show environment info
                var envInfo = Log.GetEnvironmentInfo();
                Log.Info($"Testing logging system: {envInfo}");
                
                // Enable debug logging for demonstration
                Log.EnableDebugLogging();
                
                // Test all logging levels
                Log.Debug("This is a debug message - detailed diagnostics");
                Log.Info("This is an info message - general information");
                Log.Warn("This is a warning message - something noteworthy");
                Log.Error("This is an error message - something went wrong");
                
                // Test error with exception
                try
                {
                    throw new System.InvalidOperationException("Demo exception");
                }
                catch (System.Exception ex)
                {
                    Log.Error("Caught demo exception", ex);
                }
                
                // Test log level filtering
                Log.SetQuietMode();
                Log.Info("This info message should be filtered out");
                Log.Error("This error message should still appear");
                
                // Test that logging methods don't throw exceptions
                AssertThat(true).IsTrue(); // If we get here, no exceptions were thrown
            }
            finally
            {
                // Restore original log level
                Log.SetMinimumLogLevel(originalLevel);
            }
        }
    }
}