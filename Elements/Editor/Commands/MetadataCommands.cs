using System;

namespace MLP_RiM.Elements.Editor.Commands;

public sealed class SetMetadataCommand : IEditorCommand
{
    private readonly EditorMetadataField _field;
    private readonly string _newValue;
    private string _oldValue;

    public SetMetadataCommand(EditorMetadataField field, string newValue)
    {
        _field = field;
        _newValue = newValue ?? string.Empty;
    }

    public string Name => "Set Metadata";

    public void Execute(BeatmapEditorDocument document)
    {
        _oldValue ??= document.GetMetadataField(_field);
        document.SetMetadataField(_field, _newValue);
    }

    public void Undo(BeatmapEditorDocument document)
    {
        document.SetMetadataField(_field, _oldValue);
    }
}

public sealed class SetFlashingEffectsWarningCommand : IEditorCommand
{
    private readonly bool _newValue;
    private bool _oldValue;
    private bool _hasOldValue;

    public SetFlashingEffectsWarningCommand(bool newValue)
    {
        _newValue = newValue;
    }

    public string Name => "Set Flash Warning";

    public void Execute(BeatmapEditorDocument document)
    {
        if (!_hasOldValue)
        {
            _oldValue = document.GetFlashingEffectsWarning();
            _hasOldValue = true;
        }

        document.SetFlashingEffectsWarning(_newValue);
    }

    public void Undo(BeatmapEditorDocument document)
    {
        document.SetFlashingEffectsWarning(_oldValue);
    }
}

public sealed class SetSongPathCommand : IEditorCommand
{
    private readonly string _newSongPath;
    private string _oldSongPath;

    public SetSongPathCommand(string newSongPath)
    {
        _newSongPath = newSongPath ?? string.Empty;
    }

    public string Name => "Set Song Path";

    public void Execute(BeatmapEditorDocument document)
    {
        _oldSongPath ??= document.SongPath;
        document.SetSongPath(_newSongPath, allowEmpty: true);
    }

    public void Undo(BeatmapEditorDocument document)
    {
        document.SetSongPath(_oldSongPath, allowEmpty: true);
    }
}

public sealed class ImportBeatmapAssetCommand : IEditorCommand
{
    private readonly string _sourcePath;
    private readonly string _targetSubfolder;
    private readonly EditorMetadataField _targetField;
    private string _oldValue;
    private string _importedRelativePath;

    public ImportBeatmapAssetCommand(string sourcePath, string targetSubfolder, EditorMetadataField targetField)
    {
        _sourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
        _targetSubfolder = targetSubfolder;
        _targetField = targetField;
    }

    public string Name => "Import Asset";

    public void Execute(BeatmapEditorDocument document)
    {
        _oldValue ??= document.GetMetadataField(_targetField);
        _importedRelativePath ??= document.ImportAsset(_sourcePath, _targetSubfolder);
        document.SetMetadataField(_targetField, _importedRelativePath);
    }

    public void Undo(BeatmapEditorDocument document)
    {
        document.SetMetadataField(_targetField, _oldValue);
    }
}
