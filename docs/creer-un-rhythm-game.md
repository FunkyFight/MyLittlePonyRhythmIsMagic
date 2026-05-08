# Créer un nouveau rhythm game

Le chemin normal passe par la façade `SimpleRhythmGame<TAction>`. Pour un jeu basé sur une enum d'actions, il n'est plus nécessaire d'écrire un `INotePayload`, un codec, des `EditorNoteVariant`, `CompileClip(...)` ou `GetClipTypeIdFromLegacyNote(...)` à la main.

Le noyau éditeur reste extensible : les providers sont découverts automatiquement, le clip `Switch Game` est ajouté par `EditorNoteProvider`, et les charts continuent d'utiliser des `ChartNote` runtime.

## Résumé

Pour ajouter un jeu simple, crée :

1. Une enum d'actions runtime.
2. Une classe `SimpleRhythmGame<TAction>` avec un constructeur public sans paramètre.
3. Une scène runtime qui hérite de `Scene`.
4. Des clips auteur déclarés avec le builder.

Il ne faut pas modifier `EditorNoteKind`, `Game1`, `EditorClipDefinitions` ou une liste centrale. Un nouveau jeu utilise des ids stables en `snake_case`.

## Exemple Complet

```csharp
using GameCore.Scenes;
using Microsoft.Xna.Framework;

namespace MLP_RiM.Elements.Editor;

public enum MyGameAction
{
    Basic,
    DoubleTap,
    Hold
}

public sealed class MyGameEditorNoteProvider : SimpleRhythmGame<MyGameAction>
{
    public const string GameId = "my_game";
    public const string BasicClipId = "my_game.basic";
    public const string DoubleTapClipId = "my_game.double_tap";
    public const string HoldClipId = "my_game.hold";
    public static readonly NoteTypeId TypeId = new(GameId, "note");

    protected override void Build(RhythmGameBuilder<MyGameAction> game)
    {
        game.Id(GameId)
            .DisplayName("My Game")
            .SortOrder(20)
            .Scene(() => new MyGameScene());

        game.RuntimeNote("note")
            .Input("ReactMain")
            .Occupies(0, 1)
            .HitWindow(0, 1);

        game.Clip(MyGameAction.Basic)
            .Id(BasicClipId)
            .Name("Basic")
            .Color(Color.CornflowerBlue)
            .SingleHit();

        game.Clip(MyGameAction.DoubleTap)
            .Id(DoubleTapClipId)
            .Name("Double Tap")
            .Color(Color.Gold)
            .SingleHit()
            .Pair(0.5);

        game.Clip(MyGameAction.Hold)
            .Id(HoldClipId)
            .Name("Hold")
            .Color(Color.MediumPurple)
            .Continuous(2)
            .HoldForClipLength();

        game.NoHit(1)
            .Color(Color.DimGray);
    }
}
```

La découverte par réflexion enregistre ce provider automatiquement.

## Identifiants

Les ids par défaut sont dérivés de l'enum :

```text
Basic     -> basic
DoubleTap -> double_tap
action    -> my_game_double_tap
clip      -> my_game.double_tap
variant   -> double_tap
```

Tu peux garder des constantes publiques pour les ids utilisés dans les tests, les charts ou le code runtime.

Si une ancienne chart utilise une valeur d'action non conventionnelle, ajoute un alias :

```csharp
game.LegacyActionValue(MyGameAction.DoubleTap, "old_doubletap_value");
```

Le codec générique accepte les anciennes notes sans metadata si `action` correspond à une action connue. Si `_game` ou `_type` sont présents, ils doivent correspondre au jeu.

## Note Runtime

`RuntimeNote(...)` décrit la `ChartNote` compilée :

```csharp
game.RuntimeNote("note")
    .Input("ReactMain")
    .Hold(0)
    .Occupies(0, 1)
    .HitWindow(0, 1)
    .SameVariantHitWindow(0, 0.5);
```

Pour un timing spécial, garde la façade simple et branche seulement un `IEditorNoteTiming` :

```csharp
game.RuntimeNote("note")
    .Timing(new MyGameEditorNoteTiming());
```

## Clips Standards

Un clip `SingleHit()` ou `Continuous(...)` génère une note à l'offset `0` si aucun `Emit(...)`, `Pair(...)` ou compiler custom n'est déclaré.

```csharp
game.Clip(MyGameAction.Basic)
    .SingleHit();
```

Un double tap ajoute deux emits explicites :

```csharp
game.Clip(MyGameAction.DoubleTap)
    .SingleHit()
    .Pair(0.5);
```

Un pattern répété utilise la longueur du clip et inclut la fin :

```csharp
game.Clip(MyGameAction.Basic)
    .Continuous(4)
    .LeadIn(2)
    .RepeatEvery(1);
```

Un hold peut prendre la longueur du clip auteur :

```csharp
game.Clip(MyGameAction.Hold)
    .Continuous(2)
    .HoldForClipLength();
```

Un padding complète la série après le dernier hit, utile pour des patterns en groupes :

```csharp
game.Clip(MyGameAction.Basic)
    .Continuous(3)
    .RepeatEvery(1)
    .PadToMultipleOf(4);
```

Pour un clip qui ne produit aucune note runtime :

```csharp
game.NoHit(1)
    .Color(Color.DimGray);
```

## Champs De Clips

Les options simples de clip utilisent `EditorClipFieldDefinition` et sont copiées dans les données runtime compilées.

```csharp
game.Clip(MyGameAction.Basic)
    .SingleHit()
    .Field(EditorClipFieldDefinition.Bool("my_game.big", "Big"))
    .Field(EditorClipFieldDefinition.Float("my_game.speed", "Speed", defaultValue: 1, minValue: 0.5, maxValue: 2));
```

Pour une donnée par défaut ponctuelle :

```csharp
game.Clip(MyGameAction.Basic)
    .SingleHit()
    .Data("my_game.side", "left");
```

## Compiler Custom Local

Si les patterns standards ne suffisent pas, un clip peut fournir un compiler local sans quitter la façade simple :

```csharp
game.Clip(MyGameAction.Basic)
    .SingleHit()
    .Compile((context, emit) =>
    {
        emit.Emit(0);
        emit.Emit(1);
        emit.Emit(2, holdBeats: 0.5);
    });
```

`context.StartBeat` contient le beat runtime après `LeadIn(...)`. `emit.Emit(...)` produit des `RuntimeNoteDraft`, qui sont convertis en secondes avec la `ChartTempoMap`.

## Scène Runtime

La scène reste une scène normale du moteur :

```csharp
using GameCore.Scenes;

public sealed class MyGameScene : Scene
{
    public MyGameScene() : base("My Game")
    {
    }

    public override void OnLoad()
    {
        // Créer les objets, sprites, visual notes et hooks de réaction.
    }
}
```

## Avancé

La façade simple compile vers les mêmes objets bas niveau que les anciens providers : `EditorNoteProvider`, `EditorNoteDefinition`, `EditorNoteVariant`, `INotePayload`, `RuntimeNoteDraft` et `ChartNote`.

Tu peux toujours surcharger les méthodes de `EditorNoteProvider` depuis un `SimpleRhythmGame<TAction>` si un jeu a besoin d'un comportement spécial :

```csharp
public override bool TryValidateNotes(EditorNoteValidationContext context, out string reason)
{
    reason = null;
    return true;
}
```

Cas où le bas niveau reste utile :

1. Payload custom qui ne tient pas dans une enum d'actions.
2. Codec custom avec migration complexe.
3. Provider qui génère plusieurs types de notes runtime différents.
4. Options de note legacy via `IEditorNoteOptionsPanel`.
5. Validation globale ou timing qui dépend d'une timeline complète.

Dans ces cas, écris directement un `EditorNoteProvider` comme `SeeSawEditorNote`. Pour le cas standard enum-based, utilise `SimpleRhythmGame<TAction>`.

## Checklist

- Créer l'enum d'actions.
- Créer `MyGameEditorNoteProvider : SimpleRhythmGame<MyGameAction>`.
- Déclarer `GameId`, les ids de clips publics et `TypeId` si le runtime ou les tests en ont besoin.
- Déclarer `.Id(...)`, `.DisplayName(...)`, `.SortOrder(...)` et `.Scene(...)`.
- Déclarer `.RuntimeNote(...)` avec input, occupation et hit window.
- Déclarer les clips avec `.SingleHit()`, `.Continuous(...)`, `.Pair(...)`, `.RepeatEvery(...)`, `.HoldForClipLength()` ou `.PadToMultipleOf(...)`.
- Ajouter `.NoHit(...)` si le jeu a besoin d'un bloc auteur silencieux.
- Ne pas déclarer `Switch Game` : il est ajouté automatiquement.
- Ne pas modifier les registries centraux.
