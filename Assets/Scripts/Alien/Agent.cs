using UnityEngine;

public class Agent : MonoBehaviour, VelocityProvider
{
    public Vector3 Velocity => _velocity;
    protected Vector3 _velocity;
}