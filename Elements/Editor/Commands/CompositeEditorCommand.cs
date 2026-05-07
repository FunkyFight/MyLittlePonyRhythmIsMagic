using System;
using System.Collections.Generic;
using System.Linq;

namespace MLP_RiM.Elements.Editor.Commands;

public sealed class CompositeEditorCommand : IEditorCommand
{
    private readonly IReadOnlyList<IEditorCommand> _commands;

    public CompositeEditorCommand(string name, IEnumerable<IEditorCommand> commands)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Composite" : name;
        _commands = commands?.Where(command => command != null).ToArray() ?? Array.Empty<IEditorCommand>();
    }

    public string Name { get; }

    public void Execute(BeatmapEditorDocument document)
    {
        foreach (IEditorCommand command in _commands)
            command.Execute(document);
    }

    public void Undo(BeatmapEditorDocument document)
    {
        for (int i = _commands.Count - 1; i >= 0; i--)
            _commands[i].Undo(document);
    }
}
