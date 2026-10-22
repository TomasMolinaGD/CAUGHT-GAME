using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>Identifies geometry owned by the graybox generator and its source grid.</summary>
    [DisallowMultipleComponent]
    public sealed class GeneratedGeometryMarker : MonoBehaviour
    {
        [SerializeField] private NavigationGrid sourceGrid;
        [SerializeField] private int generatorVersion = 1;

        public NavigationGrid SourceGrid => sourceGrid;
        public int GeneratorVersion => generatorVersion;

        public void Initialize(NavigationGrid grid, int version)
        {
            sourceGrid = grid;
            generatorVersion = version;
        }
    }
}
