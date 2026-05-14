using System;
using System.Collections.Generic;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class EditorNoteDefinitionBuilder
{
    private readonly NoteTypeId _typeId;
    private readonly string _displayName;
    private string _inputAction = "ReactMain";
    private double _holdBeats;
    private double _occupyBeforeBeats;
    private double _occupyAfterBeats;
    private double _hitWindowBeforeBeats;
    private double _hitWindowAfterBeats;
    private double? _sameVariantHitWindowBeforeBeats;
    private double? _sameVariantHitWindowAfterBeats;
    private readonly List<EditorNoteVariant> _variants = new();
    private IEditorNoteTiming _timing = new FixedEditorNoteTiming();
    private Func<ChartNote, bool> _matchesChartNote = _ => false;
    private IEditorNotePlacementStrategy _placementStrategy = new SingleEditorNotePlacementStrategy();

    /// <summary>
    /// Commence la description d'un type de note que l'on pourra poser dans l'éditeur.
    /// On donne ici son identité et son nom. Les autres méthodes servent ensuite à expliquer
    /// comment cette note se comporte quand on la crée, quand on la charge et quand on l'affiche.
    /// </summary>
    /// <param name="typeId">Identifiant extensible de la note. L'éditeur s'en sert pour savoir de quelle note il s'agit.</param>
    /// <param name="displayName">Nom montré au joueur dans l'éditeur.</param>
    public EditorNoteDefinitionBuilder(NoteTypeId typeId, string displayName)
    {
        _typeId = typeId;
        _displayName = displayName;
    }

    public EditorNoteDefinitionBuilder(EditorNoteKind kind, string displayName)
        : this(EditorNoteKindCompatibility.ToTypeId(kind), displayName)
    {
    }

    /// <summary>
    /// Choisit le bouton ou l'action que le joueur devra faire pour réussir cette note.
    /// Par exemple, si on met <c>ReactMain</c>, la note demandera l'action principale du jeu.
    /// Si on ne choisit rien, l'éditeur met automatiquement <c>ReactMain</c>.
    /// </summary>
    /// <param name="inputAction">Nom du bouton ou de l'action à demander au joueur.</param>
    /// <returns>Ce même constructeur, pour pouvoir continuer la description de la note.</returns>
    public EditorNoteDefinitionBuilder InputAction(string inputAction)
    {
        _inputAction = inputAction;
        return this;
    }

    /// <summary>
    /// Transforme la note en note à tenir.
    /// La valeur indique pendant combien de beats le joueur doit garder le bouton appuyé.
    /// Avec <c>0</c>, la note n'est pas une note tenue : il suffit d'appuyer au bon moment.
    /// </summary>
    /// <param name="beats">Nombre de beats pendant lesquels le bouton doit rester appuyé.</param>
    /// <returns>Ce même constructeur, pour pouvoir continuer la description de la note.</returns>
    public EditorNoteDefinitionBuilder Hold(double beats)
    {
        _holdBeats = beats;
        return this;
    }

    /// <summary>
    /// Dit quelle place la note prend sur la ligne de temps de l'éditeur.
    /// Une note peut prendre un peu de place avant le beat où elle est posée, et un peu de place après.
    /// Cette place sert à savoir où dessiner la note, où elle commence, où elle finit,
    /// et si un autre beat est déjà couvert par elle.
    /// Différence avec <see cref="HitWindow"/> : ici on parle de la taille de la note dans la chart,
    /// pas forcément du moment où elle peut être touchée ou bloquer une autre note.
    /// Exemple : une note tenue posée au beat 10 avec <c>Occupies(0, 4)</c> prend de la place du beat 10 au beat 14.
    /// Exemple : une note d'annonce posée au beat 10 avec <c>Occupies(2, 0)</c> peut être dessinée du beat 8 au beat 10.
    /// </summary>
    /// <param name="beforeBeats">Place prise avant le beat où la note est posée.</param>
    /// <param name="afterBeats">Place prise après le beat où la note est posée.</param>
    /// <returns>Ce même constructeur, pour pouvoir continuer la description de la note.</returns>
    public EditorNoteDefinitionBuilder Occupies(double beforeBeats, double afterBeats)
    {
        _occupyBeforeBeats = beforeBeats;
        _occupyAfterBeats = afterBeats;
        return this;
    }

    /// <summary>
    /// Dit autour de quel moment la note compte comme active.
    /// Cette zone sert surtout aux règles de jeu et aux conflits entre notes.
    /// Par exemple, même si une note est dessinée très petite, elle peut quand même bloquer
    /// un petit espace autour d'elle pour éviter de poser une autre note trop proche.
    /// Différence avec <see cref="Occupies"/> : ici on parle de la zone qui compte pour les interactions,
    /// pas forcément de la taille dessinée dans l'éditeur.
    /// Exemple : une note posée au beat 10 avec <c>HitWindow(0.25, 0.25)</c> compte comme active du beat 9.75 au beat 10.25.
    /// Exemple : une note qui ne doit pas être trop proche d'une autre peut utiliser <c>HitWindow(0.5, 0.5)</c>
    /// pour bloquer une demi-mesure avant et après elle, même si elle est dessinée comme une note très courte.
    /// </summary>
    /// <param name="beforeBeats">Nombre de beats comptés avant le beat de la note.</param>
    /// <param name="afterBeats">Nombre de beats comptés après le beat de la note.</param>
    /// <returns>Ce même constructeur, pour pouvoir continuer la description de la note.</returns>
    public EditorNoteDefinitionBuilder HitWindow(double beforeBeats, double afterBeats)
    {
        _hitWindowBeforeBeats = beforeBeats;
        _hitWindowAfterBeats = afterBeats;
        return this;
    }

    public EditorNoteDefinitionBuilder SameVariantHitWindow(double beforeBeats, double afterBeats)
    {
        _sameVariantHitWindowBeforeBeats = beforeBeats;
        _sameVariantHitWindowAfterBeats = afterBeats;
        return this;
    }

    /// <summary>
    /// Ajoute une version possible de cette note.
    /// Une variante sert quand le même type de note peut exister en plusieurs versions.
    /// Par exemple, une note Seapony Parade peut être une version <c>Swim</c>, <c>Star</c> ou <c>Tap Tap</c>.
    /// Cette méthode ajoute seulement le nom de la version.
    /// Elle ne crée pas automatiquement un bouton ou une liste dans le panneau d'options.
    /// </summary>
    /// <param name="displayName">Nom de la version montré dans l'éditeur.</param>
    /// <returns>Ce même constructeur, pour pouvoir continuer la description de la note.</returns>
    public EditorNoteDefinitionBuilder Variant(string displayName)
    {
        return Variant(displayName, new Dictionary<string, string>());
    }

    /// <summary>
    /// Ajoute une version possible de cette note, avec les données à mettre dans la chart.
    /// C'est utile quand plusieurs notes se ressemblent dans l'éditeur, mais doivent être sauvegardées différemment.
    /// Exemple : Seapony Parade utilise le même type de note pour <c>Swim</c>, <c>Star</c> et <c>Tap Tap</c>.
    /// La variante <c>Swim</c> peut sauvegarder les donnees produites par un codec de payload.
    /// La variante <c>Star</c> peut faire pareil avec un autre payload type.
    /// Quand l'utilisateur pose une note avec une variante, ces données sont copiées dans la nouvelle note.
    /// Attention : une variante ne s'affiche pas toute seule dans le panneau d'options.
    /// Si on veut changer cette version après avoir posé la note, le panneau d'options doit le prévoir lui-même.
    /// </summary>
    /// <param name="displayName">Nom de la version montré dans l'éditeur.</param>
    /// <param name="additionnalData">Données copiées dans la note quand cette version est utilisée.</param>
    /// <returns>Ce même constructeur, pour pouvoir continuer la description de la note.</returns>
    public EditorNoteDefinitionBuilder Variant(string displayName, IReadOnlyDictionary<string, string> additionnalData)
    {
        _variants.Add(new EditorNoteVariant(displayName, additionnalData));
        return this;
    }

    public EditorNoteDefinitionBuilder Variant(string id, string displayName, INotePayload defaultPayload, Func<INotePayload, bool> matches = null, NoteTimingPreset timingPreset = null, EditorVisualStyle editorStyle = null, EditorNoteTimingProfile timingProfile = null)
    {
        _variants.Add(new EditorNoteVariant(id, displayName, defaultPayload, matches, timingPreset, editorStyle, timingProfile));
        return this;
    }

    /// <summary>
    /// Change la façon de calculer où la note commence et où elle finit.
    /// Normalement, l'éditeur utilise simplement les valeurs données avec <see cref="Hold"/>,
    /// <see cref="Occupies"/> et <see cref="HitWindow"/>.
    /// Cette méthode sert pour les notes spéciales, quand ces calculs doivent suivre une règle différente.
    /// </summary>
    /// <param name="timing">Objet qui sait calculer les moments importants de cette note.</param>
    /// <returns>Ce même constructeur, pour pouvoir continuer la description de la note.</returns>
    public EditorNoteDefinitionBuilder Timing(IEditorNoteTiming timing)
    {
        _timing = timing;
        return this;
    }

    /// <summary>
    /// Explique comment reconnaître cette note quand elle existe déjà dans une chart.
    /// Quand l'éditeur ouvre une chart, il lit des notes sauvegardées. Cette méthode lui dit :
    /// si la note sauvegardée ressemble à ceci, alors c'est ce type de note.
    /// La règle doit être assez précise pour ne pas confondre deux types de notes différents.
    /// </summary>
    /// <param name="matchesChartNote">Règle qui répond <c>true</c> si la note sauvegardée est de ce type.</param>
    /// <returns>Ce même constructeur, pour pouvoir continuer la description de la note.</returns>
    public EditorNoteDefinitionBuilder Matches(Func<ChartNote, bool> matchesChartNote)
    {
        _matchesChartNote = matchesChartNote;
        return this;
    }

    /// <summary>
    /// Change ce qui se passe quand l'utilisateur pose cette note dans l'éditeur.
    /// Normalement, l'éditeur pose simplement la note demandée.
    /// Pour une note spéciale, on peut vouloir faire autre chose : refuser la pose,
    /// déplacer la note, ou créer plusieurs notes d'un coup.
    /// </summary>
    /// <param name="placementStrategy">Objet qui décide quoi poser réellement dans la chart.</param>
    /// <returns>Ce même constructeur, pour pouvoir continuer la description de la note.</returns>
    public EditorNoteDefinitionBuilder Placement(IEditorNotePlacementStrategy placementStrategy)
    {
        _placementStrategy = placementStrategy;
        return this;
    }

    /// <summary>
    /// Termine la description et crée l'objet utilisé par l'éditeur.
    /// Après cet appel, la note est prête à être affichée, choisie et posée.
    /// Si aucun choix n'a été ajouté avec <see cref="Variant(string)"/>, l'éditeur ajoute automatiquement
    /// un choix simple appelé <c>Default</c> pour que la note puisse quand même être utilisée.
    /// </summary>
    /// <returns>La note décrite, prête à être utilisée par l'éditeur.</returns>
    public EditorNoteDefinition Build()
    {
        IReadOnlyList<EditorNoteVariant> variants = _variants.Count > 0
            ? _variants.ToArray()
            : new[] { new EditorNoteVariant("Default", new Dictionary<string, string>()) };

        return new EditorNoteDefinition(_typeId, _displayName, _inputAction, _holdBeats, _occupyBeforeBeats, _occupyAfterBeats, _hitWindowBeforeBeats, _hitWindowAfterBeats, _sameVariantHitWindowBeforeBeats, _sameVariantHitWindowAfterBeats, variants, _timing, _matchesChartNote, _placementStrategy);
    }
}
