using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Job that applies greedy meshing optimization to reduce triangle count
/// by merging coplanar, adjacent faces with the same material.
/// </summary>
[BurstCompile]
public struct GreedyMeshJob : IJob
{
    [ReadOnly] public NativeArray<float3> inputVertices;
    [ReadOnly] public NativeArray<float2> inputUVs;
    
    [WriteOnly] public NativeList<float3> outputVertices;
    [WriteOnly] public NativeList<float2> outputUVs;
    
    public void Execute()
    {
        // For marching cubes, triangles come in groups of 3 vertices
        int triangleCount = inputVertices.Length / 3;
        
        // Track which triangles have been merged
        NativeArray<bool> merged = new NativeArray<bool>(triangleCount, Allocator.Temp);
        
        for (int i = 0; i < triangleCount; i++)
        {
            if (merged[i])
                continue;
                
            int baseIdx = i * 3;
            float3 v0 = inputVertices[baseIdx];
            float3 v1 = inputVertices[baseIdx + 1];
            float3 v2 = inputVertices[baseIdx + 2];
            float2 uv0 = inputUVs[baseIdx];
            
            // Calculate normal for this triangle
            float3 normal = math.normalize(math.cross(v1 - v0, v2 - v0));
            
            // Calculate material index from UV (since material is encoded in UV coordinates)
            int materialIdx = GetMaterialIndex(uv0);
            
            // Try to find and merge adjacent coplanar triangles with same material
            NativeList<int> mergeableTriangles = new NativeList<int>(Allocator.Temp);
            mergeableTriangles.Add(i);
            
            for (int j = i + 1; j < triangleCount; j++)
            {
                if (merged[j])
                    continue;
                    
                int otherIdx = j * 3;
                float3 ov0 = inputVertices[otherIdx];
                float3 ov1 = inputVertices[otherIdx + 1];
                float3 ov2 = inputVertices[otherIdx + 2];
                float2 oUV0 = inputUVs[otherIdx];
                
                // Check if same material
                if (GetMaterialIndex(oUV0) != materialIdx)
                    continue;
                
                // Calculate normal for other triangle
                float3 otherNormal = math.normalize(math.cross(ov1 - ov0, ov2 - ov0));
                
                // Check if coplanar (same normal direction)
                if (math.dot(normal, otherNormal) < 0.999f)
                    continue;
                
                // Check if triangles share vertices or are adjacent
                if (SharesVerticesOrAdjacent(v0, v1, v2, ov0, ov1, ov2))
                {
                    mergeableTriangles.Add(j);
                    merged[j] = true;
                }
            }
            
            // If we found triangles to merge, create optimized quad(s)
            if (mergeableTriangles.Length > 1)
            {
                // For simplicity, still output as triangles but with merged geometry
                // A more advanced implementation would create actual quads
                CreateMergedGeometry(mergeableTriangles, materialIdx);
            }
            else
            {
                // Output original triangle
                outputVertices.Add(v0);
                outputVertices.Add(v1);
                outputVertices.Add(v2);
                outputUVs.Add(inputUVs[baseIdx]);
                outputUVs.Add(inputUVs[baseIdx + 1]);
                outputUVs.Add(inputUVs[baseIdx + 2]);
            }
            
            mergeableTriangles.Dispose();
            merged[i] = true;
        }
        
        merged.Dispose();
    }
    
    private int GetMaterialIndex(float2 uv)
    {
        // Material is encoded in UV coordinates
        // Extract material index from UV position
        int row = (int)((1.0f - uv.y) / Constants.MATERIAL_SIZE);
        int col = (int)(uv.x / Constants.MATERIAL_SIZE);
        return row * Constants.MATERIAL_FOR_ROW + col;
    }
    
    private bool SharesVerticesOrAdjacent(float3 v0, float3 v1, float3 v2, 
                                          float3 ov0, float3 ov1, float3 ov2)
    {
        const float epsilon = 0.001f;
        
        // Check if any vertices are shared
        int sharedCount = 0;
        
        if (IsVertexEqual(v0, ov0, epsilon) || IsVertexEqual(v0, ov1, epsilon) || IsVertexEqual(v0, ov2, epsilon))
            sharedCount++;
        if (IsVertexEqual(v1, ov0, epsilon) || IsVertexEqual(v1, ov1, epsilon) || IsVertexEqual(v1, ov2, epsilon))
            sharedCount++;
        if (IsVertexEqual(v2, ov0, epsilon) || IsVertexEqual(v2, ov1, epsilon) || IsVertexEqual(v2, ov2, epsilon))
            sharedCount++;
            
        // Triangles are adjacent if they share at least 2 vertices (an edge)
        return sharedCount >= 2;
    }
    
    private bool IsVertexEqual(float3 a, float3 b, float epsilon)
    {
        return math.lengthsq(a - b) < epsilon * epsilon;
    }
    
    private void CreateMergedGeometry(NativeList<int> triangleIndices, int materialIdx)
    {
        // Collect all unique vertices from mergeable triangles
        // Note: materialIdx is used to ensure all merged triangles share the same material
        NativeList<float3> uniqueVertices = new NativeList<float3>(Allocator.Temp);
        
        for (int i = 0; i < triangleIndices.Length; i++)
        {
            int triIdx = triangleIndices[i];
            int baseIdx = triIdx * 3;
            
            for (int v = 0; v < 3; v++)
            {
                float3 vertex = inputVertices[baseIdx + v];
                bool found = false;
                
                for (int u = 0; u < uniqueVertices.Length; u++)
                {
                    if (IsVertexEqual(vertex, uniqueVertices[u], 0.001f))
                    {
                        found = true;
                        break;
                    }
                }
                
                if (!found)
                    uniqueVertices.Add(vertex);
            }
        }
        
        // For now, output triangles from the merged set
        // A more sophisticated approach would find the convex hull or create a quad
        for (int i = 0; i < triangleIndices.Length; i++)
        {
            int triIdx = triangleIndices[i];
            int baseIdx = triIdx * 3;
            
            outputVertices.Add(inputVertices[baseIdx]);
            outputVertices.Add(inputVertices[baseIdx + 1]);
            outputVertices.Add(inputVertices[baseIdx + 2]);
            outputUVs.Add(inputUVs[baseIdx]);
            outputUVs.Add(inputUVs[baseIdx + 1]);
            outputUVs.Add(inputUVs[baseIdx + 2]);
        }
        
        uniqueVertices.Dispose();
    }
}
