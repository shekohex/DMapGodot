using GdUnit4;
using static GdUnit4.Assertions;
using DMapImporter.Core.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace DMapImporter.Tests
{
    [TestSuite]
    public class LoggingTests
    {
        private ILoggerFactory? _loggerFactory;
        private ILogger<LoggingTests>? _logger;

        [BeforeTest]
        public void SetUp()
        {
            _loggerFactory = DMapLoggerFactory.CreateDefault();
            _logger = _loggerFactory.CreateLogger<LoggingTests>();
        }

        [AfterTest]
        public void TearDown()
        {
            _loggerFactory?.Dispose();
        }

        [TestCase]
        public void TestModernLogLevels()
        {
            // Test that modern ILogger LogLevel values work
            AssertThat(LogLevel.Trace).IsEqual(LogLevel.Trace);
            AssertThat(LogLevel.Debug).IsEqual(LogLevel.Debug);
            AssertThat(LogLevel.Information).IsEqual(LogLevel.Information);
            AssertThat(LogLevel.Warning).IsEqual(LogLevel.Warning);
            AssertThat(LogLevel.Error).IsEqual(LogLevel.Error);
            AssertThat(LogLevel.Critical).IsEqual(LogLevel.Critical);
        }

        [TestCase]
        public void TestLoggingFactoryCreatesLoggers()
        {
            var factory = DMapLoggerFactory.CreateDefault();
            AssertThat(factory).IsNotNull();

            var logger = factory.CreateLogger("TestCategory");
            AssertThat(logger).IsNotNull();

            var typedLogger = factory.CreateLogger<LoggingTests>();
            AssertThat(typedLogger).IsNotNull();

            factory.Dispose();
        }

        [TestCase]
        public void TestModernLoggingMethods()
        {
            AssertThat(_logger).IsNotNull();

            // Test that modern logging methods work
            _logger!.LogDebug("Test debug message");
            _logger!.LogInformation("Test info message");
            _logger!.LogWarning("Test warning message");
            _logger!.LogError("Test error message");
            _logger!.LogCritical("Test critical message");

            // Should not throw exceptions
            AssertThat(true).IsTrue();
        }

        [TestCase]
        public void TestLoggingConfiguration()
        {
            // Test configuring logging options
            var options = new DMapLoggingOptions
            {
                MinimumLevel = LogLevel.Warning,
                EnableConsoleLogging = true,
                EnableFileLogging = false
            };

            var factory = DMapLoggerFactory.Create(options);
            AssertThat(factory).IsNotNull();

            factory.Dispose();
        }

        [TestCase]
        public void TestDevelopmentConfiguration()
        {
            var options = DMapLoggerFactory.CreateDevelopmentOptions();
            AssertThat(options).IsNotNull();
            AssertThat(options.MinimumLevel).IsEqual(LogLevel.Debug);
        }

        [TestCase]
        public void TestProductionConfiguration()
        {
            var options = DMapLoggerFactory.CreateProductionOptions();
            AssertThat(options).IsNotNull();
            AssertThat(options.MinimumLevel).IsEqual(LogLevel.Information);
            AssertThat(options.EnableConsoleLogging).IsFalse();
        }

        [TestCase]
        public void TestGodotDetection()
        {
            var isGodotAvailable = DMapLoggerFactory.IsGodotAvailable();
            // Should return a boolean without throwing
            AssertThat(isGodotAvailable).IsNotNull();
        }

        [TestCase]
        public void TestLoggingWithParameters()
        {
            AssertThat(_logger).IsNotNull();

            _logger!.LogInformation("Test message with parameter: {param}", "testValue");
            _logger!.LogError("Error with multiple params: {param1} {param2}", 123, "test");

            // Should not throw exceptions
            AssertThat(true).IsTrue();
        }

        [TestCase]
        public void TestLoggingWithException()
        {
            AssertThat(_logger).IsNotNull();

            var ex = new InvalidOperationException("Test exception");
            _logger!.LogError(ex, "Test error with exception");

            // Should not throw exceptions
            AssertThat(true).IsTrue();
        }

        [TestCase]
        public void TestLoggingScope()
        {
            AssertThat(_logger).IsNotNull();

            using (_logger!.BeginScope("TestScope"))
            {
                _logger.LogInformation("Message within scope");
                // Should complete without error
                AssertThat(true).IsTrue();
            }
        }

        [TestCase]
        public void TestFileLoggingOptions()
        {
            var fileOptions = new FileLoggingOptions
            {
                LogDirectory = "test_logs/",
                MaxFileSizeBytes = 1024 * 1024, // 1MB
                MaxLogFiles = 3,
                EnableRotation = true
            };

            AssertThat(fileOptions.LogDirectory).IsEqual("test_logs/");
            AssertThat(fileOptions.MaxFileSizeBytes).IsEqual(1024 * 1024);
            AssertThat(fileOptions.MaxLogFiles).IsEqual(3);
            AssertThat(fileOptions.EnableRotation).IsTrue();
        }

        [TestCase]
        public void TestGodotLoggingOptions()
        {
            var godotOptions = new GodotLoggingOptions
            {
                UseGodotColors = true,
                UseRichText = false,
                IncludeScopes = true
            };

            AssertThat(godotOptions.UseGodotColors).IsTrue();
            AssertThat(godotOptions.UseRichText).IsFalse();
            AssertThat(godotOptions.IncludeScopes).IsTrue();
        }

        [TestCase]
        public void TestLoggingProviderCreation()
        {
            var fileOptions = new FileLoggingOptions();
            var fileProvider = new RotatingFileLoggerProvider(fileOptions);
            AssertThat(fileProvider).IsNotNull();
            fileProvider.Dispose();

            var godotOptions = new GodotLoggingOptions();
            var godotProvider = new GodotLoggerProvider(godotOptions);
            AssertThat(godotProvider).IsNotNull();
            godotProvider.Dispose();
        }

        [TestCase]
        public void TestLoggerIsEnabled()
        {
            AssertThat(_logger).IsNotNull();

            // Information should be enabled by default
            AssertThat(_logger!.IsEnabled(LogLevel.Information)).IsTrue();

            // Trace should be disabled by default
            AssertThat(_logger.IsEnabled(LogLevel.Trace)).IsFalse();
        }

        [TestCase]
        public void TestLoggerFactoryInstance()
        {
            var instance1 = DMapLoggerFactory.Instance;
            var instance2 = DMapLoggerFactory.Instance;

            AssertThat(instance1).IsNotNull();
            AssertThat(instance1).IsEqual(instance2); // Should be singleton
        }
    }
}