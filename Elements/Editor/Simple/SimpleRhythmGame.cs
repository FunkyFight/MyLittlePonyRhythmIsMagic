using System;
using System.Collections.Generic;
using GameCore.Scenes;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

/// <summary>
/// Provider editeur declaratif pour un rhythm game simple base sur une enum d'actions.
/// </summary>
/// <typeparam name="TAction">Enum qui liste les actions runtime du rhythm game.</typeparam>
public abstract class SimpleRhythmGame<TAction> : EditorNoteProvider
    where TAction : struct, Enum
{
    private RhythmGameDefinition<TAction> _simpleDefinition;

    /// <summary>
    /// Declare l'identite, la note runtime, les clips auteur et la scene du rhythm game.
    /// </summary>
    /// <param name="game">Builder declaratif a configurer.</param>
    protected abstract void Build(RhythmGameBuilder<TAction> game);

    /// <summary>
    /// Ordre de tri du provider dans l'editeur.
    /// </summary>
    public override int SortOrder => SimpleDefinition.SortOrder;

    /// <summary>
    /// Identifiant stable du rhythm game.
    /// </summary>
    public override string RhythmGameId => SimpleDefinition.RhythmGameId;

    /// <summary>
    /// Nom affiche du rhythm game.
    /// </summary>
    public override string RhythmGameDisplayName => SimpleDefinition.RhythmGameDisplayName;

    /// <summary>
    /// Definition de note runtime generee depuis la declaration simple.
    /// </summary>
    public override EditorNoteDefinition Definition => SimpleDefinition.EditorDefinition;

    /// <summary>
    /// Definition simple construite paresseusement apres la fin du constructeur derive.
    /// </summary>
    protected RhythmGameDefinition<TAction> SimpleDefinition => _simpleDefinition ??= CreateSimpleDefinition();

    /// <summary>
    /// Cree la scene runtime declaree par le rhythm game.
    /// </summary>
    /// <returns>Nouvelle scene runtime, ou <c>null</c> si aucune scene n'est declaree.</returns>
    public override Scene CreateScene()
    {
        return SimpleDefinition.CreateScene();
    }

    /// <summary>
    /// Compile un clip auteur simple en notes runtime.
    /// </summary>
    /// <param name="clip">Clip auteur a compiler.</param>
    /// <param name="tempoMap">Tempo map utilisee pour convertir les beats en secondes.</param>
    /// <returns>Notes runtime generees.</returns>
    public override IReadOnlyList<ChartNote> CompileClip(ChartEditorClip clip, ChartTempoMap tempoMap)
    {
        if (clip == null || tempoMap == null || !IsRuntimeClip(clip, out EditorClipDefinition definition))
            return Array.Empty<ChartNote>();

        Dictionary<string, string> data = CreateClipData(clip, definition);
        return SimpleDefinition.CompileClip(clip, definition, tempoMap, data);
    }

    /// <summary>
    /// Retrouve le clip auteur correspondant a une note runtime legacy.
    /// </summary>
    /// <param name="note">Note runtime legacy.</param>
    /// <returns>Identifiant du clip auteur correspondant.</returns>
    public override string GetClipTypeIdFromLegacyNote(ChartNote note)
    {
        return SimpleDefinition.GetClipTypeIdFromLegacyNote(note);
    }

    /// <summary>
    /// Retrouve l'index de variant d'une note runtime depuis son action enum.
    /// </summary>
    /// <param name="note">Note runtime a analyser.</param>
    /// <returns>Index du variant, ou <c>0</c> par defaut.</returns>
    public override int GetNoteVariantIndex(ChartNote note)
    {
        return SimpleDefinition.GetNoteVariantIndex(note);
    }

    /// <summary>
    /// Retourne le style editeur associe a une note runtime.
    /// </summary>
    /// <param name="note">Note runtime a styler.</param>
    /// <returns>Style du variant correspondant.</returns>
    public override EditorVisualStyle GetEditorStyle(ChartNote note)
    {
        return SimpleDefinition.GetEditorStyle(note);
    }

    /// <summary>
    /// Cree les clips auteur declares par le builder simple.
    /// </summary>
    /// <returns>Definitions de clips auteur, hors clip <c>Switch Game</c> automatique.</returns>
    protected override IReadOnlyList<EditorClipDefinition> CreateClips()
    {
        return SimpleDefinition.EditorClips;
    }

    private RhythmGameDefinition<TAction> CreateSimpleDefinition()
    {
        RhythmGameBuilder<TAction> builder = new();
        Build(builder);
        return builder.BuildDefinition();
    }
}
