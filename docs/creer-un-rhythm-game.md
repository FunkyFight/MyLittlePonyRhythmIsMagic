# Créer un nouveau rhythm game

Cette page décrit le chemin attendu pour ajouter un nouveau jeu dans l'éditeur. L'objectif est que toute la déclaration du jeu soit centralisée dans un `EditorNoteProvider`.

## Résumé

Pour ajouter un jeu, il faut créer :

1. Une scène runtime qui hérite de `Scene`.
2. Un `EditorNoteProvider` qui déclare le jeu, ses clips, ses notes et sa scène.
3. Une valeur dans `EditorNoteKind` pour identifier la définition de note côté éditeur.

Il ne faut pas modifier `Game1`, `EditorClipDefinitions` ou la liste des rhythm games. Les providers sont découverts automatiquement.

## Fichier principal

Crée un provider dans `Elements/Editor/Notes/<TonJeu>/`, par exemple `MyGameEditorNote.cs`.

```csharp
using System.Collections.Generic;
using GameCore.Scenes;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class MyGameEditorNote : EditorNoteProvider
{
    public const string GameId = "my_game";
    public const string BasicClipId = "my_game.basic";
    public const string HoldClipId = "my_game.hold";

    private const string BasicAction = "my_game_basic";
    private const string HoldAction = "my_game_hold";

    public override int SortOrder => 20;

    public override string RhythmGameId => GameId;

    public override string RhythmGameDisplayName => "My Game";

    public override EditorNoteDefinition Definition { get; } = new EditorNoteDefinitionBuilder(EditorNoteKind.MyGame, "My Game")
        .InputAction("ReactMain")
        .Occupies(beforeBeats: 1, afterBeats: 1)
        .HitWindow(beforeBeats: 0, afterBeats: 1)
        .Matches(note => note.AdditionnalData != null
            && note.AdditionnalData.TryGetValue("action", out string action)
            && action.StartsWith("my_game_"))
        .Variant("Basic", new Dictionary<string, string> { ["action"] = BasicAction })
        .Variant("Hold", new Dictionary<string, string> { ["action"] = HoldAction })
        .Build();

    public override Scene CreateScene()
    {
        return new MyGameScene();
    }

    protected override IReadOnlyList<EditorClipDefinition> CreateClips()
    {
        return new[]
        {
            Clip(BasicClipId, "Basic", EditorClipCategory.SingleHit, 0, "ReactMain", new Dictionary<string, string>
            {
                ["action"] = BasicAction
            }),
            Clip(HoldClipId, "Hold", EditorClipCategory.Continuous, 2, "ReactMain", new Dictionary<string, string>
            {
                ["action"] = HoldAction
            })
        };
    }
}
```

## Ce que le provider déclare

`SortOrder` contrôle l'ordre d'affichage dans la liste des jeux.

`RhythmGameId` est l'identifiant stable du jeu. Utilise du `snake_case`, par exemple `my_game`.

`RhythmGameDisplayName` est le nom visible dans l'éditeur.

`Definition` décrit les notes runtime du jeu : timing, occupation, hit window, variants, matching XML.

`CreateScene()` crée la scène runtime du jeu. `Game1` utilise cette méthode automatiquement quand un marker `Switch Game` est atteint.

`CreateClips()` déclare la palette du jeu. Le clip `Switch Game` est ajouté automatiquement en première entrée, donc il ne faut pas le déclarer ici.

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

## Ajouter la valeur EditorNoteKind

Ajoute une valeur dans `Elements/Editor/EditorNoteDefinition.cs` :

```csharp
public enum EditorNoteKind
{
    RhythmInput,
    SeeSaw,
    SeaponyParade,
    MyGame
}
```

C'est actuellement la seule déclaration hors provider nécessaire, parce que l'éditeur utilise `EditorNoteKind` comme clé interne pour sélectionner les notes et options.

## Clips et catégories

Utilise `Clip(...)` dans `CreateClips()` :

```csharp
Clip("my_game.basic", "Basic", EditorClipCategory.SingleHit, 0, "ReactMain", data)
```

Les catégories disponibles :

- `SingleHit` : clip court qui génère une note ou un groupe de notes ponctuel.
- `Continuous` : clip avec durée, utile pour générer une série de notes.
- `NoHit` : bloc de timeline qui ne génère aucune note de réaction.
- `Instant` : marker instantané. Le `Switch Game` automatique utilise cette catégorie.
- `TempoChange` : réservé pour les effets de tempo si on veut les unifier avec les clips.

## Compiler des clips spéciaux

Par défaut, un clip runtime génère une seule `ChartNote` au beat de départ du clip.

Si le jeu a des clips continus ou des règles spéciales, override `CompileClip(...)` :

```csharp
public override IReadOnlyList<ChartNote> CompileClip(ChartEditorClip clip, ChartTempoMap tempoMap)
{
    if (clip == null || tempoMap == null || !IsRuntimeClip(clip, out EditorClipDefinition definition))
        return Array.Empty<ChartNote>();

    Dictionary<string, string> data = CreateClipData(clip, definition);
    return clip.ClipTypeId switch
    {
        HoldClipId => CompileContinuous(clip, tempoMap, data, stepBeats: 1.0),
        _ => base.CompileClip(clip, tempoMap)
    };
}
```

Helpers utiles fournis par `EditorNoteProvider` :

- `CreateClipData(...)` fusionne les données par défaut du clip avec ses données éditées.
- `CreateRuntimeNote(...)` crée une `ChartNote` au beat donné.
- `CompileContinuous(...)` crée une série de notes sur la durée du clip.
- `IsRuntimeClip(...)` filtre automatiquement les clips `Switch Game`, `NoHit` et `Instant`.

## Options de note

Si les notes ont des options éditables, crée un panel :

```csharp
public override IEditorNoteOptionsPanel OptionsPanel { get; } = new MyGameNoteOptionsPanel();
```

Si le jeu n'a pas d'options, ne surcharge pas `OptionsPanel`.

## Migration depuis des anciennes notes

Quand un vieux chart contient déjà des `ChartNote`, l'éditeur essaie de retrouver le clip correspondant avec les `DefaultData` déclarées dans les clips.

Si ce mapping automatique ne suffit pas, override `GetClipTypeIdFromLegacyNote(...)` :

```csharp
public override string GetClipTypeIdFromLegacyNote(ChartNote note)
{
    if (note?.AdditionnalData != null
        && note.AdditionnalData.TryGetValue("action", out string action)
        && action == HoldAction)
        return HoldClipId;

    return BasicClipId;
}
```

## Checklist

- Créer `MyGameScene`.
- Ajouter `EditorNoteKind.MyGame`.
- Créer `MyGameEditorNote : EditorNoteProvider`.
- Déclarer `RhythmGameId`, `RhythmGameDisplayName`, `SortOrder`.
- Déclarer `Definition` avec ses variants et son `Matches(...)`.
- Retourner la scène dans `CreateScene()`.
- Déclarer les clips dans `CreateClips()`.
- Override `CompileClip(...)` seulement si les clips ne sont pas de simples single hits.
- Ne pas modifier `Game1`.
- Ne pas modifier `EditorClipDefinitions` pour ajouter la palette du jeu.
- Ne pas déclarer manuellement `Switch Game` : il est ajouté automatiquement.
