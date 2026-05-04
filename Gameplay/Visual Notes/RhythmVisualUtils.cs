using System;
using System.Collections.Generic;
using GameCore.Animation;
using GameCore.GameObjects;
using Microsoft.Xna.Framework;

/// <summary>
/// Regroupe les helpers generiques utilises par les notes visuelles rythmiques deterministes.
/// </summary>
/// <remarks>
/// Une VisualNote doit idealement reconstruire son etat visuel depuis la position courante de
/// la musique, et non depuis le temps ecoule entre deux frames. Ces helpers centralisent les
/// calculs reutilisables pour ce modele: fenetres d'approche, detection de retour arriere,
/// progression de phase, arcs de saut, declencheurs one-shot et changement force d'animation.
/// La classe ne contient aucune regle propre a un mini-jeu precis.
/// </remarks>
public static class RhythmVisualUtils
{
    /// <summary>
    /// Ecart minimal, en secondes, a partir duquel une baisse de position musicale est traitee
    /// comme un rewind plutot que comme une petite imprecision numerique.
    /// </summary>
    public const double DefaultRewindThreshold = 0.001;

    /// <summary>
    /// Indique si la position de la musique se trouve dans la time window où la visual note est censée bouger
    /// </summary>
    /// <param name="currentSongPosition">Position musicale courante, en secondes.</param>
    /// <param name="noteSongPosition">Position musicale de la note de référence, en secondes</param>
    /// <param name="approachDuration">Durée du visuel d'approche avant hit, en secondes</param>
    /// <param name="despawnDelay">Durée du visuel de post-hit, en secondes</param>
    /// <returns><c>true</c> si la position se trouve dans l'intervalle</returns>
    public static bool IsInTimeWindow(double currentSongPosition, double noteSongPosition, double approachDuration, double despawnDelay, bool despawnIncluded = false)
    {
        if(!despawnIncluded) return currentSongPosition >= noteSongPosition - approachDuration && currentSongPosition < noteSongPosition + despawnDelay;
        if(despawnIncluded) return currentSongPosition >= noteSongPosition - approachDuration && currentSongPosition <= noteSongPosition + despawnDelay;
        return false;
    }

    /// <summary>
    /// Indique si la lecture musicale est revenue en arriere depuis la derniere mise a jour.
    /// </summary>
    /// <param name="currentSongPosition">Position musicale courante, en secondes.</param>
    /// <param name="lastSongPosition">Position musicale observee a la frame precedente.</param>
    /// <param name="threshold">Tolerance anti-bruit avant de considerer le mouvement comme un rewind.</param>
    /// <returns><c>true</c> si la position courante est inferieure a la precedente au-dela de la tolerance.</returns>
    public static bool HasRewound(double currentSongPosition, double lastSongPosition, double threshold = DefaultRewindThreshold)
    {
        return !double.IsNaN(lastSongPosition) && currentSongPosition < lastSongPosition - threshold;
    }

    /// <summary>
    /// Calcule le debut de la fenetre d'approche d'une note.
    /// </summary>
    /// <param name="hitTime">Instant ou la note doit etre touchee, en secondes.</param>
    /// <param name="approachDuration">Duree d'approche avant le hit, en secondes.</param>
    /// <returns>L'instant de debut de l'approche.</returns>
    public static double GetApproachStart(double hitTime, double approachDuration)
    {
        return hitTime - approachDuration;
    }

    /// <summary>
    /// Indique si une position musicale se situe avant la fenetre d'approche d'une note.
    /// </summary>
    /// <param name="currentSongPosition">Position musicale courante, en secondes.</param>
    /// <param name="hitTime">Instant ou la note doit etre touchee, en secondes.</param>
    /// <param name="approachDuration">Duree d'approche avant le hit, en secondes.</param>
    /// <returns><c>true</c> si la note ne devrait pas encore appliquer d'etat visuel d'approche.</returns>
    public static bool IsBeforeApproach(double currentSongPosition, double hitTime, double approachDuration)
    {
        return currentSongPosition < GetApproachStart(hitTime, approachDuration);
    }

    /// <summary>
    /// Indique si une position musicale a atteint ou depasse le temps de hit d'une note.
    /// </summary>
    /// <param name="currentSongPosition">Position musicale courante, en secondes.</param>
    /// <param name="hitTime">Instant ou la note doit etre touchee, en secondes.</param>
    /// <returns><c>true</c> si l'etat final de la note doit etre applique.</returns>
    public static bool IsAtOrAfterHit(double currentSongPosition, double hitTime)
    {
        return currentSongPosition >= hitTime;
    }

    /// <summary>
    /// Calcule la progression normalisee et bornee d'une fenetre d'approche.
    /// </summary>
    /// <param name="currentSongPosition">Position musicale courante, en secondes.</param>
    /// <param name="hitTime">Instant ou la note doit etre touchee, en secondes.</param>
    /// <param name="approachDuration">Duree d'approche avant le hit, en secondes.</param>
    /// <returns>Une progression bornee entre <c>0</c> et <c>1</c>.</returns>
    /// <remarks>
    /// Utiliser cette methode pour positionner un objet dans une animation qui ne doit pas sortir
    /// de sa fenetre. Pour detecter un depassement avant/apres fenetre, utiliser plutot
    /// <see cref="GetUnclampedApproachProgress"/>.
    /// </remarks>
    public static float GetApproachProgress(double currentSongPosition, double hitTime, double approachDuration)
    {
        return Math.Clamp(GetUnclampedApproachProgress(currentSongPosition, hitTime, approachDuration), 0f, 1f);
    }

    /// <summary>
    /// Calcule la progression brute d'une fenetre d'approche, sans bornage.
    /// </summary>
    /// <param name="currentSongPosition">Position musicale courante, en secondes.</param>
    /// <param name="hitTime">Instant ou la note doit etre touchee, en secondes.</param>
    /// <param name="approachDuration">Duree d'approche avant le hit, en secondes.</param>
    /// <returns>
    /// Une progression ou <c>0</c> correspond au debut d'approche et <c>1</c> au hit. La valeur
    /// peut etre inferieure a <c>0</c> ou superieure a <c>1</c> si la position est hors fenetre.
    /// </returns>
    /// <remarks>
    /// Si la duree d'approche vaut zero ou moins, la methode evite la division par zero et renvoie
    /// directement <c>0</c> avant le hit ou <c>1</c> a partir du hit.
    /// </remarks>
    public static float GetUnclampedApproachProgress(double currentSongPosition, double hitTime, double approachDuration)
    {
        if (approachDuration <= 0)
            return currentSongPosition >= hitTime ? 1f : 0f;

        return (float)((currentSongPosition - GetApproachStart(hitTime, approachDuration)) / approachDuration);
    }

    /// <summary>
    /// Convertit une progression globale en progression locale pour une sous-phase.
    /// </summary>
    /// <param name="progression">Progression globale, generalement entre <c>0</c> et <c>1</c>.</param>
    /// <param name="phaseStart">Debut normalise de la sous-phase.</param>
    /// <param name="phaseEnd">Fin normalisee de la sous-phase.</param>
    /// <returns>La progression de la sous-phase, bornee entre <c>0</c> et <c>1</c>.</returns>
    /// <remarks>
    /// Cette methode sert aux choregraphies decoupees en plusieurs temps: par exemple un premier
    /// acteur qui saute sur la phase <c>[0, 0.5]</c>, puis un second sur <c>[0.5, 1]</c>.
    /// Si la phase est vide ou inversee, le resultat bascule de <c>0</c> a <c>1</c> au seuil de fin.
    /// </remarks>
    public static float GetPhaseProgress(float progression, float phaseStart, float phaseEnd)
    {
        if (phaseEnd <= phaseStart)
            return progression >= phaseEnd ? 1f : 0f;

        return Math.Clamp((progression - phaseStart) / (phaseEnd - phaseStart), 0f, 1f);
    }

    /// <summary>
    /// Evalue un guard d'ownership optionnel avant de modifier un etat visuel partage.
    /// </summary>
    /// <param name="canApplyState">Predicate fourni par la scene proprietaire, ou <c>null</c> si aucun guard n'est necessaire.</param>
    /// <returns><c>true</c> si l'appelant peut appliquer ses mutations visuelles.</returns>
    /// <remarks>
    /// Les VisualNotes peuvent coexister pendant le look-ahead/look-behind. Un guard permet a la
    /// scene de designer une seule note autorisee a muter des GameObjects partages pendant une frame.
    /// </remarks>
    public static bool CanApplyState(Func<bool> canApplyState)
    {
        return canApplyState == null || canApplyState();
    }

    /// <summary>
    /// Transforme un booleen en declencheur one-shot.
    /// </summary>
    /// <param name="triggered">Reference vers le flag qui memorise si l'evenement a deja ete declenche.</param>
    /// <returns><c>true</c> uniquement lors du premier appel tant que le flag n'a pas ete reinitialise.</returns>
    /// <remarks>
    /// Utile pour les changements d'animation de type evenement, comme <c>jump</c> ou <c>land</c>,
    /// qui doivent se produire une fois par passage vers l'avant mais etre rejouables apres un seek.
    /// </remarks>
    public static bool TrySetTrigger(ref bool triggered)
    {
        if (triggered)
            return false;

        triggered = true;
        return true;
    }

    /// <summary>
    /// Calcule le multiplicateur vertical d'un arc sinusoidal.
    /// </summary>
    /// <param name="progression">Progression de l'arc. La valeur est bornee entre <c>0</c> et <c>1</c>.</param>
    /// <returns><c>0</c> au debut et a la fin, <c>1</c> au milieu de l'arc.</returns>
    public static float SineArcHeight(float progression)
    {
        return (float)Math.Sin(Math.Clamp(progression, 0f, 1f) * Math.PI);
    }

    /// <summary>
    /// Interpole une position le long d'un arc sinusoidal deterministe.
    /// </summary>
    /// <param name="from">Position de depart au sol.</param>
    /// <param name="to">Position d'arrivee au sol.</param>
    /// <param name="height">Hauteur maximale de l'arc, en pixels.</param>
    /// <param name="progression">Progression de l'arc. La valeur est bornee entre <c>0</c> et <c>1</c>.</param>
    /// <returns>Position interpolee avec deplacement horizontal lineaire et hauteur sinusoidale.</returns>
    /// <remarks>
    /// Dans MonoGame, l'axe Y augmente vers le bas. La hauteur est donc soustraite a Y pour faire
    /// monter l'objet visuellement.
    /// </remarks>
    public static Vector2 SineArc(Vector2 from, Vector2 to, float height, float progression)
    {
        progression = Math.Clamp(progression, 0f, 1f);
        Vector2 basePos = Vector2.Lerp(from, to, progression);
        return new Vector2(basePos.X, basePos.Y - height * SineArcHeight(progression));
    }

    /// <summary>
    /// Applique directement a un GameObject une position calculee par <see cref="SineArc"/>.
    /// </summary>
    /// <param name="target">Objet a deplacer. Si la valeur est <c>null</c>, la methode ne fait rien.</param>
    /// <param name="from">Position de depart au sol.</param>
    /// <param name="to">Position d'arrivee au sol.</param>
    /// <param name="height">Hauteur maximale de l'arc, en pixels.</param>
    /// <param name="progression">Progression de l'arc. La valeur est bornee entre <c>0</c> et <c>1</c>.</param>
    public static void ApplySineArc(GameObject target, Vector2 from, Vector2 to, float height, float progression)
    {
        if (target == null)
            return;

        target.Position = SineArc(from, to, height, progression);
    }

    /// <summary>
    /// Indique si une position est plus proche d'une reference que d'une autre.
    /// </summary>
    /// <param name="value">Position a comparer.</param>
    /// <param name="a">Premiere reference.</param>
    /// <param name="b">Seconde reference.</param>
    /// <returns><c>true</c> si <paramref name="value"/> est strictement plus proche de <paramref name="a"/>.</returns>
    /// <remarks>
    /// En cas d'egalite parfaite, la methode renvoie <c>false</c> et choisit donc implicitement la
    /// seconde reference. Ce comportement est volontairement stable pour les calculs deterministes.
    /// </remarks>
    public static bool IsNearer(Vector2 value, Vector2 a, Vector2 b)
    {
        return Vector2.Distance(value, a) < Vector2.Distance(value, b);
    }

    /// <summary>
    /// Calcule une duree d'approche a partir de la reference la plus proche du point de depart.
    /// </summary>
    /// <param name="beatDuration">Duree d'un beat, en secondes.</param>
    /// <param name="from">Position de depart a evaluer.</param>
    /// <param name="shortReference">Reference associee a la duree courte.</param>
    /// <param name="longReference">Reference associee a la duree longue.</param>
    /// <param name="shortBeats">Nombre de beats si <paramref name="from"/> est plus proche de la reference courte.</param>
    /// <param name="longBeats">Nombre de beats sinon.</param>
    /// <returns>La duree d'approche en secondes.</returns>
    public static double ApproachDurationByNearestReference(double beatDuration, Vector2 from, Vector2 shortReference, Vector2 longReference, double shortBeats, double longBeats)
    {
        return beatDuration * (IsNearer(from, shortReference, longReference) ? shortBeats : longBeats);
    }

    /// <summary>
    /// Force l'etat courant d'une machine d'animation, avec support optionnel de re-entree.
    /// </summary>
    /// <param name="stateMachine">Machine d'animation a modifier. Si elle est <c>null</c>, la methode ne fait rien.</param>
    /// <param name="stateName">Nom de l'etat cible.</param>
    /// <param name="reenterViaState">
    /// Etat intermediaire a forcer avant l'etat cible lorsque la machine est deja dans l'etat cible.
    /// Laisser <c>null</c> pour ignorer les demandes redondantes.
    /// </param>
    /// <remarks>
    /// Certaines animations doivent pouvoir redemarrer meme si leur etat est deja actif, par exemple
    /// un <c>land</c> joue deux fois de suite. Dans ce cas, passer un etat intermediaire, typiquement
    /// <c>jump</c>, force une sortie/re-entree observable par la machine.
    /// </remarks>
    public static void ForceAnimationState(AnimationStateMachine stateMachine, string stateName, string reenterViaState = null)
    {
        if (stateMachine == null)
            return;

        if (stateMachine.CurrentState?.Name == stateName && reenterViaState == null)
            return;

        if (stateMachine.CurrentState?.Name == stateName && reenterViaState != null)
            stateMachine.ForceState(reenterViaState);

        stateMachine.ForceState(stateName);
    }

    /// <summary>
    /// Force l'etat d'une machine d'animation stockee dans un dictionnaire indexe par identifiant.
    /// </summary>
    /// <typeparam name="TKey">Type de la cle utilisee pour retrouver la machine.</typeparam>
    /// <param name="states">Dictionnaire des machines d'animation. Si la valeur est <c>null</c>, la methode ne fait rien.</param>
    /// <param name="key">Cle de la machine a modifier.</param>
    /// <param name="stateName">Nom de l'etat cible.</param>
    /// <param name="reenterViaState">Etat intermediaire optionnel utilise pour forcer une re-entree.</param>
    public static void ForceAnimationState<TKey>(IReadOnlyDictionary<TKey, AnimationStateMachine> states, TKey key, string stateName, string reenterViaState = null)
    {
        if (states == null || !states.TryGetValue(key, out AnimationStateMachine stateMachine))
            return;

        ForceAnimationState(stateMachine, stateName, reenterViaState);
    }

    /**
    * Computes the normalized progression of a value between a start and an end.
    *
    * The returned value is clamped between 0 and 1:
    * - returns 0 when current is before or equal to start
    * - returns a value between 0 and 1 when current is between start and end
    * - returns 1 when current is after or equal to end
    *
    * Example:
    * GetProgression(10, 14, 12) returns 0.5
    *
    * If end is lower than or equal to start, the interval is considered invalid.
    * In that case, the function returns 0 before the end point and 1 once the
    * current value reaches or passes it.
    *
    * @param start   The beginning of the interval.
    * @param end     The end of the interval.
    * @param current The current value to evaluate.
    *
    * @return A normalized progression value between 0 and 1.
    */
    public static double GetProgression(double start, double end, double current)
    {
        if (end <= start)
            return current >= end ? 1.0 : 0.0;

        return Math.Clamp((current - start) / (end - start), 0.0, 1.0);
    }
}
