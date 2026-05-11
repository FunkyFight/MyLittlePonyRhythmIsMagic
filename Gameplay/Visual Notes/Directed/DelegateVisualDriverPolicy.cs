using System;
using Rhythm.Note;

/// <summary>
/// Policy de driver basée sur une lambda.
/// </summary>
public sealed class DelegateVisualDriverPolicy : IVisualDriverPolicy
{
    private readonly Func<VisualDriverContext, Note> _resolver;

    /// <summary>
    /// Crée une policy qui délègue la résolution à une fonction.
    /// </summary>
    public DelegateVisualDriverPolicy(Func<VisualDriverContext, Note> resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <inheritdoc />
    public Note ResolveDriver(VisualDriverContext context)
    {
        return _resolver(context);
    }
}
