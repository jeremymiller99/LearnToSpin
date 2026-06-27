using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// Drop this on an empty GameObject in any scene and press Play (or use the
    /// "LearnToSpin/Setup Prototype In Current Scene" menu). It procedurally builds
    /// the whole launch prototype — runway, tire, chase camera, light, HUD — so we
    /// can iterate on gameplay before committing to prefabs/scene authoring.
    ///
    /// This class is just the configuration + orchestration: it holds every tunable
    /// and the editor-wired art prefab references, then hands them to the focused
    /// builders in Build/ (GroundBuilder, HazardBuilder, TireBuilder, RigBuilder,
    /// DecorBuilder). Each builder falls back to primitives when its art isn't wired,
    /// so the prototype still runs with nothing assigned.
    ///
    /// The terrain is endless: rather than building a fixed-length runway, the
    /// GameDirector spins up a <see cref="WorldStreamer"/> that generates terrain
    /// chunks around the tire and recycles them as it rolls, so a run never ends by
    /// reaching the edge of the world.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Tire")]
        [Tooltip("Fallback size/mass/bounce used only when no TireCatalog is wired (the equipped " +
                 "tire from the catalog normally drives these).")]
        public float tireRadius = 0.5f;
        public float tireMass = 18f;
        [Range(0f, 1f)] public float tireBounciness = 0.55f;

        [Header("Shop / progression")]
        [Tooltip("The buyable tires + upgrade tracks + payout rates. Wired by the LearnToSpin setup " +
                 "menu. If left empty a default 12-tire catalog is generated in code (plain-cylinder " +
                 "tires) so the loop still runs.")]
        public TireCatalog tireCatalog;

        [Header("World")]
        [Tooltip("The world now streams endlessly (see WorldStreamer) so a run never reaches an end. " +
                 "This only sets the camera's far-clip ceiling for the live [ ] view-distance tweak.")]
        public float runwayLength = 2000f;
        [Tooltip("Seed for the endless terrain layout. Each chunk is seeded from this, so the same " +
                 "seed reproduces the same world. Change it for a different run of terrain.")]
        public int worldSeed = 1337;
        [Tooltip("Half-width of the visual road / fence line, metres from centre.")]
        public float roadHalfWidth = 14f;
        [Tooltip("Centre-to-centre road tile spacing as a fraction of tile length. " +
                 "Below 1 overlaps tiles slightly to close the seams between them.")]
        [Range(0.7f, 1f)] public float roadTileSpacing = 0.92f;
        [Tooltip("Use the sand_road meshes themselves as the ground collider instead of a flat " +
                 "plane. More authentic, but the mesh surface may roll less smoothly than the plane.")]
        public bool useRoadMeshColliders = false;

        [Header("Performance — view distance (lower = faster)")]
        [Tooltip("How far the world renders, metres. Drives the camera far-clip plane plus matching " +
                 "distance fog so distant scenery fades out instead of popping. Lower this on weak " +
                 "machines / WebGL to cut draw calls. Live-tweak in Play with [ and ].")]
        [Range(50f, 2000f)] public float viewDistance = 450f;
        [Tooltip("Fraction of the view distance the world stays clear before fog begins. " +
                 "Lower = thicker, earlier fog.")]
        [Range(0.1f, 0.95f)] public float fogStartFraction = 0.55f;
        [Tooltip("Fog / horizon colour — keep it close to the sky so the fade is invisible.")]
        public Color fogColor = new Color(0.74f, 0.80f, 0.88f);

        [Header("Ground apron (cosmetic sand under/around the path)")]
        [Tooltip("Half-width of the sand apron, metres. Should reach well past the fence line " +
                 "(roadHalfWidth) so the world doesn't look like it's floating.")]
        public float groundApronHalfWidth = 80f;
        [Tooltip("How far the apron runs past each end of the runway, metres.")]
        public float groundApronEndPad = 90f;
        [Tooltip("Apron tile size, metres. Larger = fewer meshes (it's distant background sand).")]
        public float groundApronCell = 60f;
        [Tooltip("How much bigger each apron tile is than its grid cell. >1 makes neighbouring " +
                 "tiles overlap so there are no gaps between them. 1.25 = 25% overlap.")]
        [Range(1f, 1.6f)] public float groundApronOverlap = 1.25f;

        [Header("Art prefabs (auto-wired by the LearnToSpin setup menu)")]
        [Tooltip("sand_road_* tiles — laid flat to cover the runway.")]
        public GameObject[] roadPrefabs;
        [Tooltip("Trees, rocks, barrels and props scattered as trigger hazards.")]
        public GameObject[] hazardPrefabs;
        [Tooltip("white_fence_side — tiled down both edges of the road.")]
        public GameObject fencePrefab;
        [Tooltip("lamp — placed every 100 m as a distance marker.")]
        public GameObject lampPrefab;
        [Tooltip("Launch ramp model. Rendered on top of an invisible angled box collider (the box " +
                 "stays the physics surface). Falls back to a plain wedge cube when left empty.")]
        public GameObject rampPrefab;
        [Tooltip("Decorative trees scattered outside the fences (set dressing only).")]
        public GameObject[] sceneryTreePrefabs;
        [Tooltip("Decorative bushes scattered outside the fences (set dressing only).")]
        public GameObject[] sceneryBushPrefabs;
        [Tooltip("Decorative ivy scattered outside the fences (set dressing only).")]
        public GameObject[] sceneryIvyPrefabs;

        void Awake()
        {
            var grip = new PhysicsMaterial("Grip")
            {
                dynamicFriction = 1.1f,
                staticFriction = 1.2f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum
            };

            RigBuilder.BuildLighting();

            // The director owns the tire + camera + HUD + shop and the run→shop→run loop, plus the
            // WorldStreamer that builds the endless terrain around the tire. It needs the tire to
            // exist before it can stream the world, so it does both (see GameDirector.Boot).
            var director = new GameObject("GameDirector").AddComponent<GameDirector>();
            director.Boot(this, grip);
        }
    }
}
