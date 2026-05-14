using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MLP_RiM.Elements.Editor;

/// <summary>
/// Builder declaratif d'un clip auteur dans un <see cref="SimpleRhythmGame{TAction}"/>.
/// </summary>
/// <typeparam name="TAction">Enum qui liste les actions runtime du rhythm game.</typeparam>
public sealed class SimpleClipBuilder<TAction>
    where TAction : struct, Enum
{
    private readonly SimpleClipConfiguration<TAction> _configuration;

    internal SimpleClipBuilder(SimpleClipConfiguration<TAction> configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Definit l'identifiant stable du clip auteur.
    /// </summary>
    /// <param name="clipTypeId">Identifiant stable du clip.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> Id(string clipTypeId)
    {
        _configuration.ClipTypeId = clipTypeId;
        return this;
    }

    /// <summary>
    /// Definit le nom affiche du clip auteur.
    /// </summary>
    /// <param name="displayName">Nom lisible dans l'editeur.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> Name(string displayName)
    {
        _configuration.DisplayName = displayName;
        return this;
    }

    /// <summary>
    /// Definit la couleur d'affichage du clip et de son variant runtime.
    /// </summary>
    /// <param name="color">Couleur d'affichage.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> Color(Color color)
    {
        _configuration.EditorStyle = new EditorVisualStyle(color);
        return this;
    }

    /// <summary>
    /// Definit le style d'affichage du clip et de son variant runtime.
    /// </summary>
    /// <param name="style">Style d'affichage.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> Style(EditorVisualStyle style)
    {
        _configuration.EditorStyle = style;
        return this;
    }

    /// <summary>
    /// Declare le clip comme un hit ponctuel.
    /// </summary>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> SingleHit()
    {
        _configuration.Category = EditorClipCategory.SingleHit;
        _configuration.DefaultLengthBeats = 0.0;
        return this;
    }

    /// <summary>
    /// Declare le clip comme un bloc continu avec une longueur par defaut.
    /// </summary>
    /// <param name="defaultLengthBeats">Longueur par defaut du clip en beats.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> Continuous(double defaultLengthBeats)
    {
        _configuration.Category = EditorClipCategory.Continuous;
        _configuration.DefaultLengthBeats = Math.Max(0.0, defaultLengthBeats);
        return this;
    }

    /// <summary>
    /// Definit l'espace occupe par les notes runtime produites par ce clip sur la timeline de l'editeur.
    /// </summary>
    /// <param name="beforeBeats">Nombre de beats occupes avant le beat de chaque note produite.</param>
    /// <param name="afterBeats">Nombre de beats occupes apres le beat de chaque note produite.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> Occupies(double beforeBeats, double afterBeats)
    {
        _configuration.HasOccupies = true;
        _configuration.OccupyBeforeBeats = Math.Max(0.0, beforeBeats);
        _configuration.OccupyAfterBeats = Math.Max(0.0, afterBeats);
        return this;
    }

    /// <summary>
    /// Definit la fenetre de hit et de conflit des notes runtime produites par ce clip.
    /// </summary>
    /// <param name="beforeBeats">Nombre de beats avant le beat de chaque note produite.</param>
    /// <param name="afterBeats">Nombre de beats apres le beat de chaque note produite.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> HitWindow(double beforeBeats, double afterBeats)
    {
        _configuration.HasHitWindow = true;
        _configuration.HitWindowBeforeBeats = Math.Max(0.0, beforeBeats);
        _configuration.HitWindowAfterBeats = Math.Max(0.0, afterBeats);
        return this;
    }

    /// <summary>
    /// Definit la fenetre de conflit entre notes du meme variant produites par ce clip.
    /// </summary>
    /// <param name="beforeBeats">Nombre de beats avant le beat de chaque note produite.</param>
    /// <param name="afterBeats">Nombre de beats apres le beat de chaque note produite.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> SameVariantHitWindow(double beforeBeats, double afterBeats)
    {
        _configuration.HasSameVariantHitWindow = true;
        _configuration.SameVariantHitWindowBeforeBeats = Math.Max(0.0, beforeBeats);
        _configuration.SameVariantHitWindowAfterBeats = Math.Max(0.0, afterBeats);
        return this;
    }

    /// <summary>
    /// Ajoute un champ configurable au clip auteur.
    /// </summary>
    /// <param name="field">Definition du champ a afficher dans l'editeur.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> Field(EditorClipFieldDefinition field)
    {
        if (field != null)
            _configuration.Fields.Add(field);

        return this;
    }

    /// <summary>
    /// Ajoute plusieurs champs configurables au clip auteur.
    /// </summary>
    /// <param name="fields">Definitions de champs a afficher dans l'editeur.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> Fields(params EditorClipFieldDefinition[] fields)
    {
        if (fields == null)
            return this;

        foreach (EditorClipFieldDefinition field in fields)
            Field(field);

        return this;
    }

    /// <summary>
    /// Ajoute plusieurs champs configurables au clip auteur.
    /// </summary>
    /// <param name="fields">Definitions de champs a afficher dans l'editeur.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> Fields(IEnumerable<EditorClipFieldDefinition> fields)
    {
        if (fields == null)
            return this;

        foreach (EditorClipFieldDefinition field in fields)
            Field(field);

        return this;
    }

    /// <summary>
    /// Ajoute une donnee legacy par defaut au clip auteur.
    /// </summary>
    /// <param name="key">Cle de donnee.</param>
    /// <param name="value">Valeur par defaut.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> Data(string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(key))
            _configuration.Data[key] = value ?? string.Empty;

        return this;
    }

    /// <summary>
    /// Decale les notes runtime generees apres le debut du clip auteur.
    /// </summary>
    /// <param name="beats">Decalage en beats.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> LeadIn(double beats)
    {
        _configuration.LeadInBeats = Math.Max(0.0, beats);
        return this;
    }

    /// <summary>
    /// Ajoute une emission explicite de note runtime dans le pattern du clip.
    /// </summary>
    /// <param name="offsetBeats">Offset en beats depuis le debut runtime du pattern.</param>
    /// <param name="holdBeats">Duree tenue de cette emission en beats.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> Emit(double offsetBeats, double holdBeats = 0.0)
    {
        _configuration.Emits.Add(new SimpleClipEmit(offsetBeats, Math.Max(0.0, holdBeats), holdBeats > 0.0));
        return this;
    }

    /// <summary>
    /// Ajoute deux emissions, a l'offset <c>0</c> puis a l'offset indique.
    /// </summary>
    /// <param name="offsetBeats">Offset de la deuxieme emission en beats.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> Pair(double offsetBeats)
    {
        return Emit(0.0).Emit(offsetBeats);
    }

    /// <summary>
    /// Repete le pattern d'emissions sur toute la longueur positive du clip.
    /// </summary>
    /// <param name="beats">Pas de repetition en beats.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> RepeatEvery(double beats)
    {
        _configuration.RepeatEveryBeats = beats > 0.0 ? beats : null;
        return this;
    }

    /// <summary>
    /// Alias lisible de <see cref="RepeatEvery"/> pour les patterns en paires.
    /// </summary>
    /// <param name="beats">Pas de repetition en beats.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> RepeatPairsEvery(double beats)
    {
        return RepeatEvery(beats);
    }

    /// <summary>
    /// Complete la serie generee pour que sa longueur soit un multiple donne.
    /// </summary>
    /// <param name="multiple">Multiple de notes vise.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> PadToMultipleOf(int multiple)
    {
        _configuration.PadToMultiple = multiple > 1 ? multiple : 0;
        return this;
    }

    /// <summary>
    /// Utilise la longueur du clip auteur comme duree tenue des emissions sans hold explicite.
    /// </summary>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> HoldForClipLength()
    {
        _configuration.HoldForClipLength = true;
        return this;
    }

    /// <summary>
    /// Remplace le compiler standard de ce clip par une fonction locale.
    /// </summary>
    /// <param name="compile">Fonction appelee pour emettre les notes runtime du clip.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public SimpleClipBuilder<TAction> Compile(Action<SimpleClipCompileContext<TAction>, SimpleRuntimeNoteEmitter<TAction>> compile)
    {
        _configuration.CustomCompiler = compile;
        return this;
    }
}

internal sealed class SimpleClipConfiguration<TAction>
    where TAction : struct, Enum
{
    public SimpleClipConfiguration(TAction action, bool isRuntime)
    {
        Action = action;
        IsRuntime = isRuntime;
        Category = isRuntime ? EditorClipCategory.SingleHit : EditorClipCategory.NoHit;
    }

    public TAction Action { get; }
    public bool IsRuntime { get; }
    public string ClipTypeId { get; set; }
    public string DisplayName { get; set; }
    public EditorClipCategory Category { get; set; }
    public double DefaultLengthBeats { get; set; }
    public EditorVisualStyle EditorStyle { get; set; }
    public bool HasOccupies { get; set; }
    public double OccupyBeforeBeats { get; set; }
    public double OccupyAfterBeats { get; set; }
    public bool HasHitWindow { get; set; }
    public double HitWindowBeforeBeats { get; set; }
    public double HitWindowAfterBeats { get; set; }
    public bool HasSameVariantHitWindow { get; set; }
    public double SameVariantHitWindowBeforeBeats { get; set; }
    public double SameVariantHitWindowAfterBeats { get; set; }
    public List<EditorClipFieldDefinition> Fields { get; } = new();
    public Dictionary<string, string> Data { get; } = new();
    public double LeadInBeats { get; set; }
    public List<SimpleClipEmit> Emits { get; } = new();
    public double? RepeatEveryBeats { get; set; }
    public int PadToMultiple { get; set; }
    public bool HoldForClipLength { get; set; }
    public Action<SimpleClipCompileContext<TAction>, SimpleRuntimeNoteEmitter<TAction>> CustomCompiler { get; set; }
}

internal readonly struct SimpleClipEmit
{
    public SimpleClipEmit(double offsetBeats, double holdBeats, bool hasExplicitHoldBeats)
    {
        OffsetBeats = offsetBeats;
        HoldBeats = holdBeats;
        HasExplicitHoldBeats = hasExplicitHoldBeats;
    }

    public double OffsetBeats { get; }
    public double HoldBeats { get; }
    public bool HasExplicitHoldBeats { get; }
}
