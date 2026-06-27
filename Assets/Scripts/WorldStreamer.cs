using System.Collections.Generic;
using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// Turns the world into an endless runner. Instead of building the whole runway up front, it
    /// keeps a window of fixed-length terrain "chunks" generated around the tire — a few ahead and
    /// one behind — and recycles chunks once the tire has rolled past them. The flat physics floor
    /// is one long plane re-centred under the tire every frame, so the ground never runs out.
    ///
    /// Each chunk's contents (road, apron, ramps, fences, lamps, hazards, scenery) are produced by
    /// the focused Build*Slice methods over the chunk's z-range, seeded by the chunk index so the
    /// same stretch always looks the same — even after it's recycled and regenerated. Owned and
    /// re-targeted by <see cref="GameDirector"/> so each fresh run streams from the start again.
    /// </summary>
    public class WorldStreamer : MonoBehaviour
    {
        [Tooltip("Length of one streamed terrain chunk, metres.")]
        public float chunkLength = 200f;
        [Tooltip("How many chunks to keep generated ahead of the tire.")]
        public int chunksAhead = 3;
        [Tooltip("How many chunks to keep behind the tire before recycling them.")]
        public int chunksBehind = 1;

        GameBootstrap _boot;
        PhysicsMaterial _grip;
        Transform _target;
        GameObject _floor;

        // Seed for THIS run's terrain. Combines the world-style seed with the day so every day
        // streams a brand-new layout, while still being deterministic within a run (recycled
        // chunks regenerate identically). Set by the GameDirector each time it (re)targets.
        int _runSeed;

        // Cached prefab metrics so every chunk tiles on the same global grid (seamless boundaries).
        float _roadCellZ;
        bool _hasFence;
        float _fenceSegLen;
        Quaternion _fenceRot;

        readonly Dictionary<int, Transform> _chunks = new();
        readonly List<int> _toRecycle = new();

        /// <summary>Wire up the world and build the first window of chunks around the tire.</summary>
        public void Init(GameBootstrap boot, PhysicsMaterial grip, Transform target, int runSeed)
        {
            _boot = boot;
            _grip = grip;
            _target = target;
            _runSeed = runSeed;

            _roadCellZ = GroundBuilder.RoadCellZ(boot);
            _hasFence = DecorBuilder.FenceMetric(boot, out _fenceSegLen, out _fenceRot);
            _floor = GroundBuilder.BuildPhysicsFloor(boot, grip);

            Refresh();   // ground + scenery exist before the first launch
            MoveFloor();
        }

        /// <summary>Point at a freshly rebuilt tire and regenerate the world from its start with a
        /// new layout seed (a new day = a new world).</summary>
        public void Retarget(Transform target, int runSeed)
        {
            _target = target;
            _runSeed = runSeed;
            foreach (var c in _chunks.Values)
                if (c != null) Destroy(c.gameObject);
            _chunks.Clear();
            Refresh();
            MoveFloor();
        }

        void Update()
        {
            if (_target == null) return;
            Refresh();
            MoveFloor();
        }

        /// <summary>Keep the long physics plane centred under the tire so it never rolls off.</summary>
        void MoveFloor()
        {
            if (_floor == null || _target == null) return;
            Vector3 p = _floor.transform.position;
            _floor.transform.position = new Vector3(0f, p.y, _target.position.z);
        }

        /// <summary>Generate any missing chunk in the window around the tire and recycle the rest.</summary>
        void Refresh()
        {
            int cur = Mathf.FloorToInt(_target.position.z / chunkLength);
            for (int i = cur - chunksBehind; i <= cur + chunksAhead; i++) EnsureChunk(i);

            _toRecycle.Clear();
            foreach (var kv in _chunks)
                if (kv.Key < cur - chunksBehind || kv.Key > cur + chunksAhead) _toRecycle.Add(kv.Key);
            foreach (int key in _toRecycle)
            {
                if (_chunks[key] != null) Destroy(_chunks[key].gameObject);
                _chunks.Remove(key);
            }
        }

        void EnsureChunk(int i)
        {
            if (_chunks.ContainsKey(i)) return;

            var parent = new GameObject($"Chunk_{i}").transform;
            float z0 = i * chunkLength;
            float z1 = z0 + chunkLength;

            // Deterministic per-chunk layout: the same chunk index always regenerates identically,
            // even after it's recycled. Restore the global RNG so gameplay randomness is untouched.
            var prevState = Random.state;
            Random.InitState(_runSeed * 9176 + i);

            GroundBuilder.BuildApronSlice(_boot, parent, z0, z1);                      // sand underneath, first
            GroundBuilder.BuildRoadSlice(_boot, _grip, parent, z0, z1, _roadCellZ);    // path on top
            GroundBuilder.BuildRampSlice(_boot, _grip, parent, z0, z1);
            if (_hasFence) DecorBuilder.BuildFenceSlice(_boot, parent, z0, z1, _fenceSegLen, _fenceRot);
            DecorBuilder.BuildMarkersSlice(_boot, parent, z0, z1);
            HazardBuilder.BuildSlice(_boot, parent, z0, z1);
            DecorBuilder.BuildScenerySlice(_boot, parent, z0, z1);

            Random.state = prevState;
            _chunks[i] = parent;
        }
    }
}
