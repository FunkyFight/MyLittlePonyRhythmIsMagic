# Créer un nouveau rhythm game

Cette page décrit le chemin actuel pour ajouter un jeu dans l'éditeur. Le principe à respecter est simple : le noyau éditeur ne doit pas connaître ton jeu. Toute la déclaration spécifique au jeu vit dans un `EditorNoteProvider`.

## Résumé

Pour ajouter un jeu, crée :

1. Une scène runtime qui hérite de `Scene`.
2. Un payload de note typé qui implémente `INotePayload`.
3. Un codec qui lit et écrit ce payload dans `ChartNote.AdditionnalData`.
4. Un `EditorNoteProvider` qui déclare le jeu, son `NoteTypeId`, ses clips, ses variants runtime et sa scène.
5. Un `INotePatternCompiler` seulement si un clip auteur génère plusieurs notes runtime.

Il ne faut pas ajouter de valeur dans `EditorNoteKind` pour un nouveau jeu. `EditorNoteKind` existe encore pour la compatibilité legacy. Un nouveau jeu utilise `NoteTypeId(GameId, NoteId)`.

Il ne faut pas modifier `Game1`, `EditorClipDefinitions`, `BeatmapEditorElement` ou une liste centrale de rhythm games. Les providers sont découverts automatiquement.

## Identité

Choisis des identifiants stables en `snake_case` :

```csharp
public const string GameId = "my_game";
public const string NoteId = "note";
public static readonly NoteTypeId TypeId = new(GameId, NoteId);

public const string BasicClipId = "my_game.basic";
public const string HoldClipId = "my_game.hold";
```

`GameId` identifie le rhythm game. `NoteId` identifie le type de note runtime dans ce jeu. Les clips auteur ont aussi des ids stables, mais ils ne remplacent pas `NoteTypeId`.

## Payload typé

Le payload représente les données gameplay de la note runtime. Il doit savoir revenir au format XML actuel via `ToLegacyData()`.

```csharp
public enum MyGameAction
{
    Basic,
    Hold
}

public sealed record MyGameNotePayload(MyGameAction Action) : INotePayload
{
    public string GameId => MyGameNoteCodec.GameId;
    public string NoteId => MyGameNoteCodec.NoteId;
    public int SchemaVersion => MyGameNoteCodec.SchemaVersion;

    public Dictionary<string, string> ToLegacyData()
    {
        return MyGameNoteCodec.Write(this);
    }
}
```

Le codec est l'endroit où l'on garde la compatibilité avec les anciennes strings `action=...`.

```csharp
public static class MyGameNoteCodec
{
    public const string GameId = "my_game";
    public const string NoteId = "note";
    public const int SchemaVersion = 1;

    private const string BasicActionValue = "my_game_basic";
    private const string HoldActionValue = "my_game_hold";

    public static bool Matches(IReadOnlyDictionary<string, string> data)
    {
        return TryRead(data, out _);
    }

    public static MyGameNotePayload Read(IReadOnlyDictionary<string, string> data)
    {
        return TryRead(data, out MyGameNotePayload payload)
            ? payload
            : new MyGameNotePayload(MyGameAction.Basic);
    }

    public static bool TryRead(IReadOnlyDictionary<string, string> data, out MyGameNotePayload payload)
    {
        if (data != null
            && data.TryGetValue(NotePayloadKeys.Action, out string action)
            && TryParseAction(action, out MyGameAction parsed))
        {
            payload = new MyGameNotePayload(parsed);
            return true;
        }

        payload = default;
        return false;
    }

    public static Dictionary<string, string> Write(MyGameNotePayload payload)
    {
        return new Dictionary<string, string>
        {
            [NotePayloadKeys.Game] = GameId,
            [NotePayloadKeys.Type] = NoteId,
            [NotePayloadKeys.Version] = SchemaVersion.ToString(),
            [NotePayloadKeys.Action] = payload.Action == MyGameAction.Hold ? HoldActionValue : BasicActionValue
        };
    }

    private static bool TryParseAction(string value, out MyGameAction action)
    {
        switch (value)
        {
            case HoldActionValue:
                action = MyGameAction.Hold;
                return true;
            case BasicActionValue:
                action = MyGameAction.Basic;
                return true;
            default:
                action = default;
                return false;
        }
    }
}
```

## Provider

Crée un provider dans `Elements/Editor/Notes/<TonJeu>/`, par exemple `MyGameEditorNoteProvider.cs`.

```csharp
using System;
using System.Collections.Generic;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class MyGameEditorNoteProvider : EditorNoteProvider
{
    public const string GameId = MyGameNoteCodec.GameId;
    public const string BasicClipId = "my_game.basic";
    public const string HoldClipId = "my_game.hold";
    public static readonly NoteTypeId TypeId = new(GameId, MyGameNoteCodec.NoteId);

    public override int SortOrder => 20;

    public override string RhythmGameId => GameId;

    public override string RhythmGameDisplayName => "My Game";

    public override EditorNoteDefinition Definition { get; } = new EditorNoteDefinitionBuilder(TypeId, "My Game")
        .InputAction("ReactMain")
        .Occupies(beforeBeats: 0, afterBeats: 1)
        .HitWindow(beforeBeats: 0, afterBeats: 1)
        .Matches(note => MyGameNoteCodec.Matches(note?.AdditionnalData))
        .Variant("basic", "Basic", new MyGameNotePayload(MyGameAction.Basic), MatchesAction(MyGameAction.Basic), editorStyle: new EditorVisualStyle(Color.DeepSkyBlue))
        .Variant("hold", "Hold", new MyGameNotePayload(MyGameAction.Hold), MatchesAction(MyGameAction.Hold), editorStyle: new EditorVisualStyle(Color.Gold))
        .Build();

    public override Scene CreateScene()
    {
        return new MyGameScene();
    }

    public override int GetNoteVariantIndex(ChartNote note)
    {
        return MyGameNoteCodec.Read(note?.AdditionnalData).Action == MyGameAction.Hold ? 1 : 0;
    }

    protected override IReadOnlyList<EditorClipDefinition> CreateClips()
    {
        return new[]
        {
            Clip(BasicClipId, "Basic", EditorClipCategory.SingleHit, 0, "ReactMain", MyGameNoteCodec.Write(new MyGameNotePayload(MyGameAction.Basic)), editorStyle: new EditorVisualStyle(Color.CornflowerBlue)),
            Clip(HoldClipId, "Hold", EditorClipCategory.Continuous, 2, "ReactMain", MyGameNoteCodec.Write(new MyGameNotePayload(MyGameAction.Hold)), editorStyle: new EditorVisualStyle(Color.Gold)),
            Clip(EditorClipDefinitions.NoHit, "No Hit", EditorClipCategory.NoHit, 1, editorStyle: new EditorVisualStyle(Color.DimGray))
        };
    }

    private static Func<INotePayload, bool> MatchesAction(MyGameAction action)
    {
        return payload => payload is MyGameNotePayload myGamePayload && myGamePayload.Action == action;
    }
}
```

Le provider doit avoir un constructeur public sans paramètre. La découverte par réflexion s'occupe du reste.

## Variants et clips

Les clips sont les vraies entrées auteur affichées dans la palette de timeline. Un clip peut être `SingleHit`, `Continuous`, `NoHit` ou `Instant`.

Les variants de `EditorNoteDefinition` ne sont pas une palette de clips. Ils sont des presets de payload runtime pour créer, reconnaître, styler et timer une `ChartNote` déjà compilée.

Règle pratique :

1. Si l'utilisateur doit poser un bloc dans la timeline, déclare un clip dans `CreateClips()`.
2. Si une `ChartNote` runtime peut exister en plusieurs payloads, déclare un variant correspondant.
3. Si le style d'une note dépend du payload complet et pas seulement du variant, override `GetEditorStyle(ChartNote note)`.

Le clip `Switch Game` est ajouté automatiquement par `EditorNoteProvider`. Ne le déclare pas toi-même.

## Champs de clips

Si un clip a des options simples, utilise les fields déclaratifs au lieu d'un panel custom :

```csharp
private static readonly IReadOnlyList<EditorClipFieldDefinition> Fields = new[]
{
    EditorClipFieldDefinition.Bool("my_game.big", "Big"),
    EditorClipFieldDefinition.Float("my_game.speed", "Speed", defaultValue: 1, minValue: 0.5, maxValue: 2),
    EditorClipFieldDefinition.Enum("my_game.side", "Side", "left", new[]
    {
        new EditorClipFieldOption("left", "Left"),
        new EditorClipFieldOption("right", "Right")
    })
};
```

Puis passe `Fields` au helper `Clip(...)`.

## Compiler des clips

Par défaut, un clip runtime génère une seule `ChartNote` au beat de départ du clip.

Si ton clip génère un pattern, crée un `INotePatternCompiler` qui retourne des `RuntimeNoteDraft` :

```csharp
public sealed class MyGamePatternCompiler : INotePatternCompiler
{
    public IReadOnlyList<RuntimeNoteDraft> Compile(NoteAuthoringIntent intent, NoteCompileContext context)
    {
        if (intent.Payload is not MyGameNotePayload payload)
            return Array.Empty<RuntimeNoteDraft>();

        return new[]
        {
            new RuntimeNoteDraft(intent.StartBeat, payload),
            new RuntimeNoteDraft(intent.StartBeat + 1.0, payload)
        };
    }
}
```

Puis override `CompileClip(...)` dans le provider :

```csharp
public override IReadOnlyList<ChartNote> CompileClip(ChartEditorClip clip, ChartTempoMap tempoMap)
{
    if (clip == null || tempoMap == null || !IsRuntimeClip(clip, out EditorClipDefinition definition))
        return Array.Empty<ChartNote>();

    MyGameNotePayload payload = MyGameNoteCodec.Read(CreateClipData(clip, definition));
    NoteAuthoringIntent intent = new(GameId, payload.Action.ToString(), clip.StartBeat, Math.Max(0.0, clip.LengthBeats), payload);
    return _patternCompiler.Compile(intent, new NoteCompileContext(tempoMap))
        .Select(draft => draft.ToChartNote(tempoMap))
        .ToArray();
}
```

`RuntimeNoteDraft.ToChartNote(tempoMap)` convertit `Beat` et `HoldBeats` en secondes via la `TempoMap`.

## Timing spécial

Si le timing affiché ou les conflits ne suivent pas `Hold`, `Occupies` et `HitWindow`, implémente `IEditorNoteTiming` :

```csharp
public sealed class MyGameEditorNoteTiming : IEditorNoteTiming
{
    public NoteTimingResult GetTiming(NoteTimingRequest request)
    {
        return new NoteTimingResult(
            StartBeat: request.Beat,
            EndBeat: request.Beat + 1,
            HitStartBeat: request.Beat,
            HitEndBeat: request.Beat + 1,
            SameVariantHitStartBeat: request.Beat,
            SameVariantHitEndBeat: request.Beat + 1);
    }
}
```

Ajoute ensuite `.Timing(new MyGameEditorNoteTiming())` dans la definition.

## Migration depuis des anciennes notes

Quand un vieux chart contient déjà des `ChartNote`, l'éditeur retrouve le provider avec `Definition.Matches(...)`, puis tente de retrouver le clip auteur.

Si le mapping automatique par `DefaultData` ne suffit pas, override `GetClipTypeIdFromLegacyNote(...)` :

```csharp
public override string GetClipTypeIdFromLegacyNote(ChartNote note)
{
    return MyGameNoteCodec.Read(note?.AdditionnalData).Action == MyGameAction.Hold
        ? HoldClipId
        : BasicClipId;
}
```

## Options de note

Pour de petites options d'auteur, préfère les fields de clips. Un `IEditorNoteOptionsPanel` reste possible si tu dois éditer directement une `ChartNote` runtime legacy.

```csharp
public override IEditorNoteOptionsPanel OptionsPanel { get; } = new MyGameNoteOptionsPanel();
```

Si le jeu n'a pas ce besoin, ne surcharge pas `OptionsPanel`.

## Scène runtime

La scène est une scène normale du moteur :

```csharp
using GameCore.Scenes;

public sealed class MyGameScene : Scene
{
    public MyGameScene() : base("My Game")
    {
    }

    public override void OnLoad()
    {
        // Créer les objets, sprites, visual note managers, hooks de réaction, etc.
    }
}
```

## Checklist

- Créer `MyGameScene`.
- Créer `MyGameNotePayload : INotePayload`.
- Créer `MyGameNoteCodec` avec lecture legacy et écriture typée.
- Créer `MyGameEditorNoteProvider : EditorNoteProvider` avec `NoteTypeId`.
- Déclarer `RhythmGameId`, `RhythmGameDisplayName`, `SortOrder`.
- Déclarer `Definition`, ses variants runtime et son `Matches(...)`.
- Retourner la scène dans `CreateScene()`.
- Déclarer les clips dans `CreateClips()`.
- Déclarer les fields de clips si les options sont simples.
- Ajouter un `INotePatternCompiler` seulement si un clip génère un pattern.
- Override `CompileClip(...)` seulement si le comportement par défaut ne suffit pas.
- Override `GetClipTypeIdFromLegacyNote(...)` seulement si la migration automatique ne suffit pas.
- Ne pas modifier `EditorNoteKind` pour un nouveau jeu.
- Ne pas modifier `Game1`.
- Ne pas modifier `EditorClipDefinitions` pour ajouter la palette du jeu.
- Ne pas déclarer manuellement `Switch Game` : il est ajouté automatiquement.
