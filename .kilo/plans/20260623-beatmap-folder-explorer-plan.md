# Plan: vrai explorateur de dossiers pour New Beatmap

## Objectif

Remplacer la modale actuelle `New Beatmap` de l'éditeur de beatmap par un explorateur de dossiers intégré au jeu, utilisable en fullscreen/borderless, sans dépendre d'un file dialog Windows et sans imposer de taper `/`.

## Problèmes à corriger

- La modale actuelle est une simple liste DevUI, pas un explorateur pratique.
- Les dossiers contenant déjà un `chart.xml` sont des packages beatmap et ne doivent pas apparaître comme dossiers navigables.
- Le workflow doit permettre de créer facilement des groupes comme `Beatmaps/Tutos/...`.
- Aucun file dialog natif ne doit être utilisé, car il est masqué par la fenêtre MonoGame/SDL selon le mode d'affichage.
- L'utilisateur doit pouvoir créer un sous-dossier et créer la beatmap dans le dossier courant sans manipuler de chemins texte.

## Fichiers concernés

- `Elements/Editor/BeatmapEditorElement.cs`
- `Elements/Editor/BeatmapPackagePaths.cs`
- Supprimer `Elements/Editor/NativeFolderPicker.cs` si encore présent et inutilisé.

## Design UI cible

Dans `BeatmapEditorElement`, remplacer le rendu `_newBeatmapWindow.Draw(...)` par une UI custom `DrawNewBeatmapExplorer(...)`.

Structure visuelle:

- Overlay sombre plein écran derrière la fenêtre.
- Fenêtre large centrée, type explorateur de fichiers.
- Header haut:
  - titre `NEW BEATMAP`
  - chemin courant relatif, par exemple `Beatmaps / Tutos`
  - bouton `X` / `CANCEL`
- Colonne gauche:
  - raccourci `Beatmaps`
  - éventuel bouton `Parent` si pas à la racine
  - rappel `Packages are hidden` pour expliquer pourquoi les dossiers beatmap ne sont pas listés
- Panneau central:
  - liste scrollable des sous-dossiers navigables
  - icône dossier stylisée
  - hover clair
  - double-clic ou clic simple sur ligne pour entrer dans le dossier
  - état vide: `No subfolders. Create one or create the beatmap here.`
- Panneau droit:
  - `Current folder`
  - `Will create`
  - champ `New subfolder name`
  - bouton `Create Folder`
  - bouton principal `Create Beatmap Here`
  - bouton `Cancel`

## Comportement

- `New Beatmap` ouvre l'explorateur intégré à la racine `Beatmaps`.
- La liste centrale affiche uniquement les dossiers qui ne contiennent pas `chart.xml`.
- Les dossiers contenant `chart.xml` sont masqués ici, car ce sont des beatmaps et doivent rester dans `Open Beatmap`.
- `Create Folder` crée un sous-dossier dans le dossier courant puis navigue dedans.
- `Create Beatmap Here` crée `chart.xml` dans le dossier courant.
- Si le dossier courant est `Beatmaps`, créer une beatmap doit utiliser un package disponible type `Beatmaps/New Beatmap/chart.xml` ou demander/créer un sous-dossier avant confirmation selon le design choisi.
- `Esc` annule.
- `Enter` crée la beatmap si le champ texte n'est pas focus.
- `Enter` crée le sous-dossier si le champ `New subfolder name` est focus et non vide.
- Molette souris scroll la liste centrale.

## Implémentation détaillée

### 1. État interne

Ajouter dans `BeatmapEditorElement`:

```csharp
private bool _newBeatmapFolderNameFocused;
private int _newBeatmapFolderListScroll;
```

Réutiliser:

```csharp
private string _newBeatmapNameBuffer;
private string _newBeatmapFolderPath;
private long _customTextBackspaceHoldStartMs;
private long _customTextBackspaceLastRepeatMs;
```

### 2. Ouverture/fermeture

Modifier `OpenNewBeatmapModal()`:

- initialiser `_newBeatmapFolderPath = BeatmapPackagePaths.BeatmapsRoot`
- créer le dossier racine si nécessaire
- reset scroll/focus/buffer
- ne plus ouvrir `_newBeatmapWindow`

Modifier `CloseNewBeatmapModal(...)`:

- fermer seulement l'état custom `_isCreatingNewBeatmap`
- reset focus/backspace
- ne plus dépendre de `_newBeatmapWindow.Close()`

### 3. Update custom

Remplacer `HandleNewBeatmapModal()` pour ne plus appeler `_newBeatmapWindow.Update(...)`.

Ajouter:

- `HandleNewBeatmapExplorerMouse()`
- `UpdateNewBeatmapFolderNameInput()`
- `NavigateToNewBeatmapFolder(string folderPath)`
- `NavigateToParentNewBeatmapFolder()`
- `CreateNewBeatmapSubfolder()`

Le hit-test doit utiliser les mêmes rectangles que le draw:

- `GetNewBeatmapExplorerBounds()`
- `GetNewBeatmapFolderListBounds(Rectangle modal)`
- `GetNewBeatmapSidebarBounds(Rectangle modal)`
- `GetNewBeatmapActionsBounds(Rectangle modal)`
- `GetNewBeatmapFolderNameInputBounds(Rectangle actions)`

### 4. Draw custom

Modifier `DrawHud(...)`:

- ne plus dessiner `_newBeatmapWindow.Draw(...)`
- si `_isCreatingNewBeatmap`, appeler `DrawNewBeatmapExplorer(spriteBatch)`

Créer les méthodes:

- `DrawNewBeatmapExplorer(SpriteBatch spriteBatch)`
- `DrawNewBeatmapExplorerHeader(...)`
- `DrawNewBeatmapSidebar(...)`
- `DrawNewBeatmapFolderList(...)`
- `DrawNewBeatmapActions(...)`
- `DrawExplorerButton(...)`
- `DrawExplorerTextInput(...)`

### 5. Filtrage des dossiers

Modifier/assurer `GetNewBeatmapChildFolders()`:

```csharp
return Directory.GetDirectories(_newBeatmapFolderPath)
    .Where(IsInsideBeatmapsRoot)
    .Where(path => !File.Exists(BeatmapPackagePaths.GetChartPathForPackage(path)))
    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
    .ToArray();
```

### 6. Création de beatmap

Garder `BeatmapPackagePaths.GetAvailablePackageChartPathForPackagePath(...)` public.

Règle recommandée:

- Si le dossier courant est `Beatmaps`, `Create Beatmap Here` crée `Beatmaps/New Beatmap/chart.xml` via `GetAvailablePackageChartPath("New Beatmap")`.
- Si le dossier courant est un sous-dossier vide/non-package, créer `current/chart.xml`.
- Si un `chart.xml` existe déjà, utiliser le suffixe `_2`, `_3`, etc. via `GetAvailablePackageChartPathForPackagePath`.

### 7. Suppression du picker natif

Supprimer `NativeFolderPicker.cs` si présent.

Retirer toute référence à:

```csharp
NativeFolderPicker.TryPickFolder(...)
```

## Validation

Compiler:

```powershell
dotnet build -p:OutDir="C:\Users\Alexis\AppData\Local\Temp\kilo\MLP_RiM_build\"
```

Tests manuels:

- Ouvrir l'éditeur de beatmap.
- `File > New Beatmap`.
- Vérifier que la fenêtre intégrée apparaît dans le jeu.
- Créer `Tutos` puis entrer dedans.
- Créer `Intro` puis entrer dedans.
- Cliquer `Create Beatmap Here`.
- Vérifier la création de `Beatmaps/Tutos/Intro/chart.xml`.
- Revenir dans `New Beatmap` et vérifier que `Intro` n'est plus affiché comme dossier navigable.
- Vérifier que `Open Beatmap` affiche `Tutos/Intro`.
