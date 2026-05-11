# Visual Notes dirigées

Les visual notes dirigées servent à écrire la chorégraphie d'une note avec une timeline déclarative, tout en centralisant les détails dangereux : timing déterministe, ownership de ressources partagées, crossings forward-only et comportement safe en rewind/seek.

Le système vit dans `Gameplay/Visual Notes/Directed/` et repose sur `DirectedVisualNote`, `VisualTimeline`, `VisualContext` et `VisualRuntime`.

## Problème résolu

Une `VisualNote` classique peut modifier directement des objets de scène : sprites, backgrounds, machines d'animation, caméra, effets, sons. Comme `VisualNoteManager<T>` garde souvent plusieurs notes actives en même temps, deux notes peuvent vouloir écrire sur la même ressource pendant la même frame.

Avant ce système, chaque visual note devait gérer elle-même :

1. Un `Func<bool> canApplyState` pour savoir si elle avait le droit de muter la ressource.
2. Un `_lastSongPosition` pour détecter les crossings et rewinds.
3. Des flags one-shot pour éviter de rejouer des SFX ou animations.
4. Des resets spécifiques après seek/rewind.

Les visual notes dirigées gardent la liberté d'écrire une chorégraphie custom, mais déplacent ces règles dans une API commune.

## Vue d'ensemble

Une visual note dirigée fait trois choses :

1. Elle dérive de `DirectedVisualNote`.
2. Elle déclare sa chorégraphie dans `Build(VisualTimeline timeline)`.
3. Elle mute les objets de scène uniquement via des tracks enregistrées dans `VisualRuntime`.

Le cycle d'update est :

1. `VisualNoteManager<T>.Update(songPosition)` appelle `visual.Update(songPosition)`.
2. `DirectedVisualNote.Update(...)` met à jour le `VisualNoteState` de base.
3. Un `VisualContext` est construit avec la note, le song position courant, le song position précédent et les progressions.
4. La `VisualTimeline` est samplée.
5. Chaque phase active exécute ses lambdas.
6. Les mutations passent par `ctx.Mutate(...)` ou `DoOwned(...)`, donc elles respectent l'ownership et le driver courant.

`DirectedVisualNote.Update(...)` est `sealed`. Une visual note dirigée ne doit pas override `Update`; elle doit déclarer ses phases dans `Build(...)`.

## Concepts

### `DirectedVisualNote`

`DirectedVisualNote` est la base au-dessus de `VisualNote`.

Elle conserve la compatibilité avec `VisualNoteManager<T>` : spawn/despawn, `Progress`, `PostHitProgress`, `HasDespawned` et `Draw(...)` restent compatibles avec le système existant.

La timeline est construite paresseusement au premier `Update`. Cela évite d'appeler du code virtuel avant que le constructeur de la classe dérivée ait initialisé ses champs.

### `VisualTimeline`

`VisualTimeline` déclare des blocs qui seront samplés selon la position musicale.

Les blocs disponibles sont :

1. `Phase(id, startProgress, endProgress)` : phase sur la progression d'approche globale de la note.
2. `DuringApproach(id)` : raccourci pour une phase `[0, 1]` entre spawn et hit.
3. `AfterHit(id)` : phase post-hit complète.
4. `AfterHitUntilDespawn(id)` : phase active de `Note.EndSongPosition` jusqu'au despawn.
5. `StableBefore(id)` : bloc actif avant le début d'approche.
6. `StableAfter(id)` : bloc actif après la fin de la fenêtre visuelle.

Une phase reçoit un `PhaseContext`. Un bloc stable reçoit seulement le `VisualContext`.

### `VisualRuntime`

`VisualRuntime` contient les ressources partagées sous forme de tracks.

Une track est une ressource nommée : background, pony, animation state machine, caméra, beam, etc.

La scène enregistre les tracks au moment où elle crée ou recrée les visual notes :

```csharp
_visualRuntime = new VisualRuntime();
_visualRuntime.RegisterTrack("background", _infiniteScrollBg);
_visualRuntime.RegisterTrack(SeaponyVisualNote.GetPonyTrackId(i), _seaPonies[i]);
_visualRuntime.RegisterTrack(SeaponyVisualNote.GetPonyAnimationTrackId(i), _seaPoniesAnimationStates[i]);
```

Ensuite, à chaque frame, la scène désigne quelle note pilote chaque track :

```csharp
_visualRuntime.SetDriver("background", _drivingBackgroundNote);
_visualRuntime.SetDriver(SeaponyVisualNote.GetPonyTrackId(i), drivingNote);
_visualRuntime.SetDriver(SeaponyVisualNote.GetPonyAnimationTrackId(i), drivingNote);
```

Si la track n'existe pas ou si la note courante n'est pas le driver, la mutation est un no-op.

### `VisualContext`

`VisualContext` est passé aux lambdas. Il contient :

1. `Note` : note logique courante.
2. `SongPosition` et `LastSongPosition`.
3. `ApproachDuration` et `DespawnDelay`.
4. `NoteProgress`, `UnclampedNoteProgress`, `PostHitProgress`.
5. `LastNoteProgress`, `LastPostHitProgress`.
6. `HasRewound`, `IsBeforeApproach`, `IsAtOrAfterHit`.
7. Les méthodes de tracks : `CanWrite`, `TryRead`, `Read`, `Mutate`.
8. Les helpers d'évènements : `ForwardCrossed`, `ForwardCrossedProgress`, `PlaySfxOnForwardCross`, `SpawnOnForwardCross`.

### `PhaseContext`

`PhaseContext` décrit une phase active.

Il expose :

1. `GlobalStart` et `GlobalEnd`.
2. `GlobalProgress`.
3. `LocalProgress` borné entre `0` et `1`.
4. `UnclampedLocalProgress`.
5. `IsActive` et `WasActive`.
6. `JustEnteredForward`, `JustExitedForward`, `CrossedLocalForward`, `CrossedGlobalForward`.

Utilise `LocalProgress` pour animer une sous-phase. Utilise `GlobalProgress` quand l'animation doit rester calée sur la progression globale de la note.

## Ownership et drivers

Il y a deux niveaux de sécurité.

Le premier niveau est déclaratif : un bloc doit déclarer les tracks qu'il possède avec `.Owns(...)`.

```csharp
timeline.AfterHitUntilDespawn("background_scroll")
    .Owns("background")
    .Do((ctx, phase) =>
    {
        ctx.Mutate<InfiniteScrollBackground>("background", bg =>
        {
            bg.Progression = phase.LocalProgress;
        });
    });
```

Le second niveau vient de la scène : le runtime doit avoir défini la note courante comme driver de la track.

```csharp
_visualRuntime.SetDriver("background", _drivingBackgroundNote);
```

La mutation passe seulement si :

1. La track existe.
2. Le bloc courant l'a déclarée avec `.Owns(...)`.
3. Le driver de la track est exactement `ctx.Note`.
4. La ressource stockée est compatible avec le type demandé.

Pour éviter de répéter `Owns` et `Mutate`, utilise `DoOwned<T>` :

```csharp
timeline.AfterHitUntilDespawn("background_scroll")
    .DoOwned<InfiniteScrollBackground>("background", (ctx, phase, bg) =>
    {
        bg.Progression = phase.LocalProgress;
    });
```

`DoOwned<T>` appelle automatiquement `.Owns(trackId)` puis `ctx.Mutate<T>(...)`.

## Crossings forward-only

Les effets one-shot ne doivent pas être rejoués à chaque frame. Ils ne doivent pas non plus se déclencher au premier sample, ni pendant un rewind.

Utilise `ctx.ForwardCrossed(...)` pour un seuil en secondes :

```csharp
if (ctx.ForwardCrossed("bubble_sfx", ctx.Note.SongPosition - ctx.ApproachDuration))
    SFX.Play(scene, "SFX/Bubble.wav", 4);
```

Utilise `ctx.ForwardCrossedProgress(...)` pour un seuil de progression :

```csharp
if (ctx.ForwardCrossedProgress("impact", 0.75f))
    SpawnImpact();
```

La règle est :

```text
last < threshold && current >= threshold && !hasRewound && last is not NaN
```

Après un rewind, le crossing peut se produire à nouveau naturellement quand la lecture repasse le seuil vers l'avant.

## Exemple minimal

```csharp
public sealed class MyBgVisualNote : DirectedVisualNote
{
    public MyBgVisualNote(Note note, VisualRuntime runtime, double approachDuration, double despawnDelay)
        : base(note, runtime, approachDuration, despawnDelay)
    {
    }

    protected override void Build(VisualTimeline timeline)
    {
        timeline.AfterHitUntilDespawn("scroll")
            .DoOwned<InfiniteScrollBackground>("background", (ctx, phase, bg) =>
            {
                bg.Progression = MathHelper.Lerp(0f, 1f, ctx.PostHitProgress);
            });
    }
}
```

La scène doit enregistrer la track :

```csharp
_visualRuntime = new VisualRuntime();
_visualRuntime.RegisterTrack("background", _background);
```

Puis définir le driver avant l'update du manager :

```csharp
_visualRuntime.SetDriver("background", currentBackgroundNote);
_backgroundVisualNotes.Update(songPosition);
```

## Exemple SeaPony background

`SeaponyBgVisualNote` est l'exemple le plus simple du projet.

Elle déclare une phase post-hit :

```csharp
timeline.AfterHitUntilDespawn("background_scroll")
    .Owns("background")
    .DoOwned<InfiniteScrollBackground>("background", (ctx, phase, background) =>
    {
        if (!SeaponyNoteCodec.TryReadAction(ctx.Note?.AdditionnalData, out _))
            return;

        float interpolated = Interpolation.EaseOutQuart(ctx.PostHitProgress);
        background.Progression = Single.Lerp(destinationBeat - 1, destinationBeat, interpolated);
    });
```

La scène fait l'arbitrage :

```csharp
_drivingBackgroundNote = findDrivingBackgroundNote(songPosition);
_visualRuntime.SetDriver("background", _drivingBackgroundNote);
_infiniteScrollBgVisualNotes.Update(songPosition);
```

Ce pattern remplace l'ancien `Func<bool> canApplyState`.

## Exemple SeaPony acteur

Pour un acteur SeaPony, la scène enregistre deux tracks par pony :

```csharp
_visualRuntime.RegisterTrack(SeaponyVisualNote.GetPonyTrackId(i), _seaPonies[i]);
_visualRuntime.RegisterTrack(SeaponyVisualNote.GetPonyAnimationTrackId(i), _seaPoniesAnimationStates[i]);
```

Puis elle synchronise les drivers :

```csharp
_visualRuntime.SetDriver(SeaponyVisualNote.GetPonyTrackId(i), drivingNote);
_visualRuntime.SetDriver(SeaponyVisualNote.GetPonyAnimationTrackId(i), drivingNote);
```

La visual note déclare les blocs qui recouvrent son cycle :

```csharp
protected override void Build(VisualTimeline timeline)
{
    timeline.StableBefore("seapony_before_approach")
        .Owns(_seaPonyTrackId, _seaPonyAnimationTrackId)
        .Do(sample);

    timeline.DuringApproach("seapony_approach")
        .Owns(_seaPonyTrackId, _seaPonyAnimationTrackId)
        .Do((ctx, phase) => sample(ctx));

    timeline.AfterHitUntilDespawn("seapony_after_hit")
        .Owns(_seaPonyTrackId, _seaPonyAnimationTrackId)
        .Do((ctx, phase) => sample(ctx));

    timeline.StableAfter("seapony_after_despawn")
        .Owns(_seaPonyTrackId, _seaPonyAnimationTrackId)
        .Do(sample);
}
```

Ensuite la logique métier peut rester dans des helpers (`handleSwim`, `handleRoll`, `handleTapTap`) mais elle est appelée depuis la timeline déclarative.

## Rewind et état stable

Un rewind ne doit pas laisser des flags one-shot dans un état impossible.

Dans une visual note dirigée, utilise `ctx.HasRewound` :

```csharp
if (ctx.HasRewound)
{
    _jumpStarted = false;
    _landed = false;
}
```

Pour les poses, positions et états déterministes, préfère des blocs stables ou des phases qui réappliquent l'état depuis le song position courant. Un état stable doit pouvoir être recalculé après un seek direct.

Règle pratique :

1. SFX, spawns, impacts one-shot : `ForwardCrossed`.
2. Position, rotation, pose, animation déterministe : réappliquer par phase ou stable block.
3. Flags locaux : reset sur `ctx.HasRewound` ou dans `StableBefore` si la note repasse avant son approche.

## Intégration avec `VisualNoteManager`

Il ne faut pas modifier `VisualNoteManager<T>`.

`DirectedVisualNote` hérite de `VisualNote`, donc le manager continue de :

1. Spawner les visuals depuis sa factory.
2. Assigner `PreviousNote` et `NextNote`.
3. Appeler `Update(songPosition)`.
4. Retirer la visual quand `HasDespawned` devient vrai.

Attention : si la factory retourne `null`, `VisualNoteManager` met la note dans ses skipped notes. Elle ne sera pas réessayée avant reset du manager.

## Checklist pour ajouter une visual note dirigée

1. Créer une classe qui hérite de `DirectedVisualNote`.
2. Passer un `VisualRuntime` partagé dans le constructeur.
3. Enregistrer les tracks nécessaires dans la scène.
4. Mettre à jour les drivers avant l'update du manager.
5. Déclarer les blocs dans `Build(VisualTimeline timeline)`.
6. Utiliser `.Owns(...)` ou `DoOwned<T>(...)` pour chaque ressource mutée.
7. Utiliser `ctx.ForwardCrossed(...)` pour les one-shots.
8. Utiliser `ctx.HasRewound` pour reset les flags locaux.
9. Ne pas override `Update(...)`.
10. Garder `Draw(...)` vide si la visual note pilote seulement des objets de scène.

## Erreurs fréquentes

Ne pas enregistrer la track : la mutation devient un no-op.

Ne pas définir le driver avant le manager : la visual note voit l'ancien driver ou aucun driver.

Muter directement un objet partagé sans `ctx.Mutate` : l'ownership runtime est contourné.

Utiliser `ForwardCrossed` pour une pose stable : après un seek direct, la pose ne sera pas reconstruite.

Jouer un SFX directement dans une phase : il sera rejoué à chaque frame active.

Déclarer une phase trop étroite pour un état qui doit survivre après seek : utiliser plutôt un bloc stable ou réappliquer l'état dans une phase plus large.

## Tests recommandés

Pour chaque visual note dirigée, ajoute au minimum :

1. Une mutation ne s'applique pas si la track n'est pas drivé par la note courante.
2. Une mutation ne s'applique pas si la phase n'a pas déclaré `.Owns(...)`.
3. Le crossing est faux au premier sample, vrai au franchissement forward, faux en rewind.
4. Les phases post-hit ne samplent pas avant le hit ni après despawn.
5. Les états stables sont réappliqués après seek/rewind.

Pour éviter les dépendances graphiques ou audio dans les tests, utilise des cibles légères ou des builders minimaux, et évite de jouer réellement des SFX.
