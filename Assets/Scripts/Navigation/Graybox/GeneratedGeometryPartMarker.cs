using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>Identifies a direct geometry object owned by one generated graybox root.</summary>
    [DisallowMultipleComponent]
    public sealed class GeneratedGeometryPartMarker : MonoBehaviour
    {
        [SerializeField] private GeneratedGeometryMarker owner;

        public GeneratedGeometryMarker Owner => owner;

        public void Initialize(GeneratedGeometryMarker generationOwner)
        {
            owner = generationOwner;
        }
    }
}
