namespace CaughtGame.Navigation
{
    public enum NavigationPathStatus
    {
        Success,
        InvalidGrid,
        InvalidStart,
        InvalidDestination,
        InvalidAgentType,
        StartNotTraversable,
        DestinationNotTraversable,
        NoPath
    }
}
