# Bug atlas rotation / SpriteEffects : flip horizontal transformé en flip vertical

## Résumé court

Le problème observé sur `SeaPonyParade` n'est probablement pas un bug de la logique tap tap elle-même, ni un état d'animation incorrect. Le symptôme vient d'une interaction entre :

1. un sprite packé dans l'atlas avec rotation TexturePacker `90°` ;
2. `TextureRegion.ApplyAtlasCorrection(...)`, qui corrige la rotation/origine/scale ;
3. `SpriteEffects`, qui est transmis inchangé à `SpriteBatch.Draw(...)` alors que les axes locaux du sprite ont été permutés par la rotation atlas.

Résultat : un `SpriteEffects.FlipHorizontally` logique peut devenir visuellement un flip vertical pour une région atlas tournée à `90°` ou `270°`.

Dans le cas visible : les poneys visuels 2 et 4, à l'entrée de la zone d'activation tap tap, utilisent le sprite `template-pony-tap2`, qui est packé rotaté. Ils doivent regarder à gauche, mais l'effet appliqué ressort visuellement comme une tête à l'envers et le sprite continue de regarder à droite.

## Symptôme précis constaté

Pendant l'entrée dans la zone d'activation de `seapony_parade_tap_tap` :

- Ordre visuel attendu gauche-vers-droite : poneys 1, 2, 3, 4.
- Pose attendue : `uptap - downtap - uptap - downtap`.
- Orientation attendue : `droite - gauche - droite - gauche`.

Symptôme réel :

- Les poneys visuels 2 et 4 ont la tête à l'envers.
- Ils regardent encore vers la droite au lieu de regarder vers la gauche.

Ce symptôme correspond exactement à un flip vertical appliqué là où l'intention visuelle était un flip horizontal.

## Fichiers concernés

### Mini-jeu / symptôme

- `Gameplay/Visual Notes/SeaponyVisualNote.cs`
  - Applique l'orientation tap tap via `sprite.Effects`.
  - C'est là que le bug devient visible, mais ce n'est probablement pas le bon endroit pour corriger durablement.

- `Scenes/SeaPonyParade.cs`
  - Déclare les états :
    - `"uptap"` -> `MainAtlas.Template_pony_tap1`
    - `"downtap"` -> `MainAtlas.Template_pony_tap2`

### Atlas / pipeline

- `Content/atlas/main_atlas.txt`
  - Ligne observée pour `template-pony-tap1` :
    ```txt
    template-pony-tap1;0;144;49;54;84;102;116;0.4444444444444444;0.4880952380952381
    ```
    Le deuxième champ vaut `0` : pas de rotation atlas.

  - Ligne observée pour `template-pony-tap2` :
    ```txt
    template-pony-tap2;1;157;135;54;84;102;116;0.5;0.6481481481481481
    ```
    Le deuxième champ vaut `1` : ancien format TexturePacker signifiant région rotatée à `90°`.

- `MonogameLibs/Core/Core/Graphics/TextureAtlas.cs`
  - `ParseTexturePackerRotation(...)` interprète `1` comme `90` degrés :
    ```csharp
    if (rotation == 1)
        return 90;
    ```

- `MonogameLibs/Core/Core/Graphics/TextureRegion.cs`
  - `ApplyAtlasCorrection(...)` corrige la rotation atlas, l'origine et la scale.
  - Mais `SpriteEffects effects` n'est pas remappé.
  - `TextureRegion.Draw(...)` passe ensuite `effects` inchangé à `spriteBatch.Draw(...)`.

- `MonogameLibs/Core/Core/Graphics/Sprite.cs`
  - `Sprite.Draw(...)` transmet `Effects` à `Region.Draw(...)`.

- `MonogameLibs/Core/Core/GameObjects/GameObject.cs`
  - `GameObject.Draw(...)` met à jour `sprite.Scale` et `sprite.Rotation`, puis appelle `sprite.Draw(...)`.
  - Il ne modifie pas `sprite.Effects`, donc l'effet appliqué par la note visuelle est bien celui envoyé au renderer.

## Pourquoi seuls les poneys visuels 2 et 4 sont touchés à l'entrée

À l'entrée de la zone d'activation, la pose attendue est :

```txt
visuel 1 : uptap
visuel 2 : downtap
visuel 3 : uptap
visuel 4 : downtap
```

Dans `SeaPonyParade.cs` :

```csharp
"uptap"   -> MainAtlas.Template_pony_tap1
"downtap" -> MainAtlas.Template_pony_tap2
```

Or :

- `template-pony-tap1` n'est pas rotaté dans l'atlas (`rotation = 0`).
- `template-pony-tap2` est rotaté dans l'atlas (`rotation = 1`, donc `90°`).

Les poneys visuels 2 et 4 sont précisément ceux qui :

1. utilisent `downtap`, donc `template-pony-tap2` ;
2. doivent regarder à gauche ;
3. reçoivent donc un flip horizontal logique.

Comme la région est rotatée à `90°`, ce flip horizontal logique est appliqué dans les axes texture/atlas au lieu des axes écran/logiques. Visuellement, il se comporte alors comme un flip vertical.

## Ce qui n'est probablement PAS la cause

### Ce n'est pas l'état `pretap` / `posttap`

Les états corrects dans la scène sont `"uptap"` et `"downtap"`. L'ancien problème de noms d'états invalides est distinct. Ici, le symptôme apparaît alors que `downtap` est bien utilisé.

### Ce n'est pas seulement l'ordre gauche-droite des poneys

L'ordre de génération des poneys est inversé par rapport à l'ordre visuel, mais ce point explique les binômes inversés, pas une tête retournée verticalement. Une tête à l'envers implique un flip vertical ou une rotation incorrecte au rendu.

### Ce n'est pas uniquement `SeaponyVisualNote`

`SeaponyVisualNote` expose le bug parce qu'il applique `SpriteEffects.FlipHorizontally` à un sprite tap rotaté dans l'atlas. Mais si un autre sprite rotaté à `90°` utilise un flip horizontal ou vertical ailleurs, il peut rencontrer le même problème.

## Importeur ou renderer ?

À première vue, l'importeur lit correctement le fichier TexturePacker :

- `template-pony-tap2;1;...` signifie ancienne notation booléenne de rotation.
- `TextureAtlas.ParseTexturePackerRotation(...)` convertit bien `1` en `90`.

Donc le problème ne semble pas être : “l'importeur a inventé une rotation”.

Le problème est plutôt dans le contrat renderer :

- L'importeur dit : “cette région est stockée tournée à 90° dans l'atlas”.
- Le renderer corrige la rotation pour l'afficher dans son orientation logique.
- Mais le renderer oublie que, après une rotation de 90° ou 270°, les axes horizontal et vertical sont échangés.
- Il faut donc remapper `SpriteEffects.FlipHorizontally` et `SpriteEffects.FlipVertically` avant d'appeler `SpriteBatch.Draw(...)`.

## Explication technique du remapping

Pour une région non rotatée (`0°`) :

```txt
axe X texture == axe X écran
axe Y texture == axe Y écran
```

Donc :

```txt
FlipHorizontally reste FlipHorizontally
FlipVertically reste FlipVertically
```

Pour une région rotatée à `90°` ou `270°` :

```txt
axe X logique/écran correspond à l'axe Y texture
axe Y logique/écran correspond à l'axe X texture
```

Donc :

```txt
FlipHorizontally logique doit devenir FlipVertically côté SpriteBatch
FlipVertically logique doit devenir FlipHorizontally côté SpriteBatch
```

Pour une région rotatée à `180°`, les deux axes sont inversés mais pas échangés. Le remapping horizontal/vertical n'a donc normalement pas besoin de swap :

```txt
FlipHorizontally reste FlipHorizontally
FlipVertically reste FlipVertically
```

## Correction racine recommandée

Corriger au niveau `TextureRegion`, pas dans `SeaponyVisualNote`.

Idée : `ApplyAtlasCorrection(...)` ne doit pas seulement corriger `rotation`, `origin` et `scale`; il doit aussi corriger `effects`.

Pseudo-code :

```csharp
private void ApplyAtlasCorrection(
    ref float rotation,
    ref Vector2 origin,
    ref Vector2 scale,
    ref SpriteEffects effects)
{
    int atlasRotation = ((AtlasRotationDegrees % 360) + 360) % 360;

    if(atlasRotation == 0)
        return;

    if(atlasRotation == 90)
    {
        effects = SwapHorizontalAndVerticalEffects(effects);
        rotation -= MathHelper.PiOver2;
        origin = new Vector2(Width - origin.Y, origin.X);
        scale = new Vector2(scale.Y, scale.X);
        return;
    }

    if(atlasRotation == 180)
    {
        rotation -= MathHelper.Pi;
        origin = new Vector2(Width - origin.X, Height - origin.Y);
        return;
    }

    if(atlasRotation == 270)
    {
        effects = SwapHorizontalAndVerticalEffects(effects);
        rotation -= MathHelper.Pi + MathHelper.PiOver2;
        origin = new Vector2(origin.Y, Height - origin.X);
        scale = new Vector2(scale.Y, scale.X);
    }
}
```

Helper possible :

```csharp
private static SpriteEffects SwapHorizontalAndVerticalEffects(SpriteEffects effects)
{
    bool flipHorizontally = (effects & SpriteEffects.FlipHorizontally) != 0;
    bool flipVertically = (effects & SpriteEffects.FlipVertically) != 0;

    SpriteEffects remapped = SpriteEffects.None;

    if(flipHorizontally)
        remapped |= SpriteEffects.FlipVertically;

    if(flipVertically)
        remapped |= SpriteEffects.FlipHorizontally;

    return remapped;
}
```

Et dans `TextureRegion.Draw(...)` :

```csharp
public void Draw(SpriteBatch spriteBatch, Vector2 position, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)
{
    ApplyAtlasCorrection(ref rotation, ref origin, ref scale, ref effects);

    spriteBatch.Draw(
        Texture,
        position,
        SourceRectangle,
        color,
        rotation,
        origin,
        scale,
        effects,
        layerDepth
    );
}
```

## Alternative moins propre

Désactiver la rotation dans TexturePacker et régénérer l'atlas.

Avantages :

- Plus simple à raisonner.
- Plus besoin de remapper les flips sur les régions rotatées.

Inconvénients :

- Augmente potentiellement la taille de l'atlas.
- Ne corrige pas le renderer.
- Le bug peut revenir si une future atlas contient à nouveau des régions rotatées.

## Workaround local retiré

Un workaround local avait été ajouté temporairement dans Gameplay/Visual Notes/SeaponyVisualNote.cs pendant le debug pour compenser le bug uniquement sur le tap tap. Il a été retiré.

Le mini-jeu doit rester sur l’intention logique simple :

`csharp
_seaPony.sprite.Effects = isTapTapLeftFacingPony()
    ? SpriteEffects.FlipHorizontally
    : SpriteEffects.None;
`

Une fois TextureRegion corrigé, cette intention logique devrait produire le bon rendu même pour les régions atlas rotatées.

## Plan de validation recommandé

1. Fermer l'instance du jeu / debugger.
   - Sinon `bin/Debug/net9.0/MLP_RiM.dll` reste verrouillé et le build normal ne remplace pas le binaire utilisé.

2. Corriger `TextureRegion.ApplyAtlasCorrection(...)` pour remapper `SpriteEffects` sur les rotations `90°` et `270°`.

3. Retirer le workaround local dans `SeaponyVisualNote.cs`.

4. Rebuild normal :

   ```powershell
   dotnet build MLP_RiM.csproj
   ```

5. Tester en jeu l'entrée dans la zone tap tap :

   ```txt
   attendu gauche-vers-droite :
   état        : uptap - downtap - uptap - downtap
   orientation : droite - gauche - droite - gauche
   ```

6. Tester le deuxième TAP :

   ```txt
   attendu gauche-vers-droite :
   état        : downtap - uptap - downtap - uptap
   orientation : droite - gauche - droite - gauche
   ```

7. Tester un rewind dans la zone d'activation.
   - L'orientation doit rester déterministe.
   - Pas de tête retournée.
   - Pas de flip qui dépend de l'historique des frames.

8. Chercher d'autres usages de `SpriteEffects` sur des sprites potentiellement rotatés dans l'atlas.
   - La correction renderer peut changer le rendu de cas qui compensaient implicitement le bug.
   - Si un ancien code avait mis `FlipVertically` exprès pour compenser une rotation atlas, il faudra le remplacer par l'intention logique correcte.

## Conclusion

Le bug est global au rendu des régions atlas rotatées avec `SpriteEffects`, mais il a été révélé par `SeaPonyParade` parce que `template-pony-tap2` est packé à `90°` et que les poneys visuels 2 et 4 doivent recevoir un flip horizontal logique dès l'entrée dans la zone tap tap.

La correction durable doit vivre dans le pipeline atlas/render (`TextureRegion`), pas dans chaque mini-jeu.
