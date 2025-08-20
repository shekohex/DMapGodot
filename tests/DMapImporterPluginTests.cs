using GdUnit4;
using static GdUnit4.Assertions;
using static GdUnit4.Utils;
using Godot;

[TestSuite]
[RequireGodotRuntime]
public partial class DMapImporterPluginTests
{
    [TestCase]
    public void TestPluginCanBeInstantiated()
    {
        // This test ensures the plugin can be created without errors
        var plugin = AutoFree(new DMapImporterPlugin())!;
        AssertThat(plugin).IsNotNull();
        AssertThat(plugin).IsInstanceOf<EditorPlugin>();
    }

    [TestCase]
    public void TestPluginHasRequiredAttributes()
    {
        // Verify the plugin class has the Tool attribute
        var pluginType = typeof(DMapImporterPlugin);
        var toolAttribute = pluginType.GetCustomAttributes(typeof(ToolAttribute), false);
        AssertThat(toolAttribute).HasSize(1);
    }

    [TestCase]
    public void TestPluginMethodsExist()
    {
        // Verify required methods exist
        var pluginType = typeof(DMapImporterPlugin);
        var enterTreeMethod = pluginType.GetMethod("_EnterTree");
        var exitTreeMethod = pluginType.GetMethod("_ExitTree");

        AssertThat(enterTreeMethod).IsNotNull();
        AssertThat(exitTreeMethod).IsNotNull();
    }
}