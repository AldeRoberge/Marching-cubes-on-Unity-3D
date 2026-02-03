using System.Collections;
using System.Collections.Generic;
using UnityEngine;

abstract public class Biome : MonoBehaviour
{
    [Header("Noise / terrain generation")] [Tooltip("Animation curve for attenuate the height in some ranges")]
    public AnimationCurve _terrainHeightCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Tooltip("Scale of the noise map")] [Range(0.001f, 100f)]
    public float _scale = 50f;

    [Tooltip("Number of deferents relief apply to the terrain surface")] [Range(1, 5)]
    public int _octaves = 4;

    [Tooltip("Amplitude decrease of reliefs")] [Range(0.001f, 1f)]
    public float _persistance = 0.5f;

    [Tooltip("Frequency increase of reliefs")] [Range(1, 20)]
    public float _lacunarity = 2f;

    protected int isoLevel;

    public virtual void Start()
    {
        isoLevel = MeshBuilder.Instance._isoLevel;
    }

    /// <summary>
    /// Generate the chunk data
    /// </summary>
    public abstract byte[] GenerateChunkData(Vector2Int vecPos, float[] biomeMerge);
}