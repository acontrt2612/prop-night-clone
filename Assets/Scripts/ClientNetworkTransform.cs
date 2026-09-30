using Unity.Netcode.Components;

/// <summary>Owner-authoritative transform: each player moves their own character.</summary>
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}
