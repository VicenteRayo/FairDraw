namespace FairDraw.Domain;

/// <summary>
/// Lifecycle states of a draw. The normal flow is strictly linear:
/// Created -> Open -> Committing -> Revealing -> Completed.
/// Cancelled is reachable from any non-terminal state.
/// </summary>
public enum DrawState
{
    Created,
    Open,
    Committing,
    Revealing,
    Completed,
    Cancelled
}
