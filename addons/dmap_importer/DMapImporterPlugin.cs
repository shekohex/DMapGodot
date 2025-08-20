#if TOOLS
using Godot;
using System;
using DMapImporter.Importers;

[Tool]
public partial class DMapImporterPlugin : EditorPlugin
{
	private DMapImporter.Importers.DMapImporter? _dmapImporter;
	
	public override void _EnterTree()
	{
		_dmapImporter = new DMapImporter.Importers.DMapImporter();
		AddImportPlugin(_dmapImporter);
		GD.Print("[DMapImporterPlugin] DMAP Importer plugin registered");
	}

	public override void _ExitTree()
	{
		if (_dmapImporter != null)
		{
			RemoveImportPlugin(_dmapImporter);
			_dmapImporter = null;
			GD.Print("[DMapImporterPlugin] DMAP Importer plugin unregistered");
		}
	}
}
#endif
