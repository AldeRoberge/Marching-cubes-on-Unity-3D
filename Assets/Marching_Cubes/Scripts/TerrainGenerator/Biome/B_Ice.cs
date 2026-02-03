using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BIce : Biome
{
    [Tooltip("The max deep and height of the desert dunes, low values")] [Range(0, Constants.MAX_HEIGHT - 1)]
    public int _maxHeightDifference = Constants.MAX_HEIGHT / 5;

    [Tooltip("Number vertex (y), where the snow end and the rock start")] [Range(0, Constants.MAX_HEIGHT - 1)]
    public int _snowDeep = Constants.MAX_HEIGHT / 5;

    [Header("Ice columns configuration")] [Tooltip("Scale of the noise used for the ice columns appear")] [Range(0, 100)]
    public int _iceNoiseScale = 40;

    [Tooltip("Value in the ice noise map where the ice columns appear")] [Range(0, 1)]
    public float _iceApearValue = 0.8f;

    [Tooltip("Ice columns max height")] [Range(0, Constants.MAX_HEIGHT - 1)]
    public int _iceMaxHeight = 5;

    [Tooltip("Amplitude decrease of reliefs")] [Range(0.001f, 1f)]
    public float _icePersistance = 0.5f;

    [Tooltip("Frequency increase of reliefs")] [Range(1, 20)]
    public float _iceLacunarity = 2f;

    public override byte[] GenerateChunkData(Vector2Int vecPos, float[] biomeMerge)
    {
        byte[] chunkData = new byte[Constants.CHUNK_BYTES];
        float[] noise = NoiseManager.GenerateNoiseMap(_scale, _octaves, _persistance, _lacunarity, vecPos);
        float[] iceNoise = NoiseManager.GenerateNoiseMap(_iceNoiseScale, 2, _icePersistance, _iceLacunarity, vecPos);
        for (int z = 0; z < Constants.CHUNK_VERTEX_SIZE; z++)
        {
            for (int x = 0; x < Constants.CHUNK_VERTEX_SIZE; x++)
            {
                // Get surface height of the x,z position 
                float height = Mathf.Lerp(
                    NoiseManager.Instance._worldConfig._surfaceLevel, //Biome merge height
                    (((_terrainHeightCurve.Evaluate(noise[x + z * Constants.CHUNK_VERTEX_SIZE]) * 2 - 1) * _maxHeightDifference) + NoiseManager.Instance._worldConfig._surfaceLevel), //Desired biome height
                    biomeMerge[x + z * Constants.CHUNK_VERTEX_SIZE]); //Merge value,0 = full merge, 1 = no merge

                int heightY = Mathf.CeilToInt(height); //Vertex Y where surface start
                int lastVertexWeigh = (int)((255 - isoLevel) * (height % 1) + isoLevel); //Weigh of the last vertex

                //Ice calculations
                int iceExtraHeigh = 0;
                if (iceNoise[x + z * Constants.CHUNK_VERTEX_SIZE] > _iceApearValue)
                    iceExtraHeigh = Mathf.CeilToInt((1 - iceNoise[x + z * Constants.CHUNK_VERTEX_SIZE]) / _iceApearValue * _iceMaxHeight);


                for (int y = 0; y < Constants.CHUNK_VERTEX_HEIGHT; y++)
                {
                    int index = (x + z * Constants.CHUNK_VERTEX_SIZE + y * Constants.CHUNK_VERTEX_AREA) * Constants.CHUNK_POINT_BYTE;
                    if (y < heightY - _snowDeep)
                    {
                        chunkData[index] = 255;
                        chunkData[index + 1] = 4; //Rock
                    }
                    else if (y < heightY + iceExtraHeigh)
                    {
                        chunkData[index] = 255;
                        if (y <= heightY)
                            chunkData[index + 1] = 3; //snow
                        else
                            chunkData[index + 1] = 5; //ice
                    }
                    else if (y == heightY + iceExtraHeigh)
                    {
                        chunkData[index] = (byte)lastVertexWeigh;
                        if (y <= heightY)
                            chunkData[index + 1] = 3; //snow
                        else
                            chunkData[index + 1] = 5; //ice
                    }
                    else
                    {
                        chunkData[index] = 0;
                        chunkData[index + 1] = Constants.NUMBER_MATERIALS;
                    }
                }
            }
        }

        return chunkData;
    }
}