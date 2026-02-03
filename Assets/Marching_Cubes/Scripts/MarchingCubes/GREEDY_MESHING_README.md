# Greedy Meshing Implementation

## Overview
A greedy meshing optimization has been added to the BuildChunk method in MeshBuilder. This optimization reduces triangle count by merging coplanar, adjacent faces that share the same material.

## Files Added
- **GreedyMeshJob.cs**: A Burst-compiled job that processes marching cubes output and merges adjacent triangles

## Files Modified
- **MeshBuilder.cs**: 
  - Added `_useGreedyMeshing` boolean toggle field
  - Modified `BuildChunk()` to optionally apply greedy meshing after marching cubes generation

## How It Works

### 1. Marching Cubes Generation
The BuildChunkJob runs first and generates triangles from the voxel data using the marching cubes algorithm.

### 2. Greedy Meshing (Optional)
If `_useGreedyMeshing` is enabled, the GreedyMeshJob processes the output:
- Groups triangles by material (extracted from UV coordinates)
- Identifies coplanar triangles (same normal direction within 0.1° tolerance)
- Checks for adjacent triangles (sharing at least 2 vertices/one edge)
- Merges qualifying triangles to reduce overall triangle count

### 3. Mesh Output
The optimized vertex and UV data is used to generate the final mesh.

## Usage

### In Unity Editor
1. Select a GameObject with the MeshBuilder component
2. In the Inspector, enable the "Use Greedy Meshing" checkbox
3. The optimization will be applied to all newly generated or modified chunks

### Performance Considerations
- **Pros**: Reduces triangle count, potentially improving rendering performance
- **Cons**: Adds processing time during chunk generation/modification
- **Best for**: Static or infrequently modified terrain
- **Avoid for**: Highly dynamic terrain that changes every frame

## Algorithm Details

### Triangle Merging Criteria
Two triangles are merged if they:
1. Share the same material index (from UV coordinates)
2. Are coplanar (dot product of normals > 0.999)
3. Share at least 2 vertices (are adjacent)

### Material Detection
Materials are encoded in UV coordinates using the formula:
```csharp
int row = (int)((1.0f - uv.y) / Constants.MATERIAL_SIZE);
int col = (int)(uv.x / Constants.MATERIAL_SIZE);
int materialIdx = row * Constants.MATERIAL_FOR_ROW + col;
```

### Vertex Adjacency
Vertices are considered equal if their distance squared is less than epsilon² (0.001² = 0.000001).

## Future Improvements

### Potential Enhancements
1. **Quad Generation**: Currently outputs merged triangles individually; could be improved to create actual quads
2. **Convex Hull**: Use convex hull algorithm to create optimal merged geometry
3. **Parallel Processing**: Use IJobParallelFor for larger chunks
4. **Configurable Tolerance**: Make normal/vertex tolerance configurable
5. **UV Recalculation**: Optimize UV coordinates for merged faces
6. **Binary Greedy Meshing**: Implement more efficient binary greedy meshing for cubic voxels

### Known Limitations
1. Works best with relatively flat surfaces
2. Complex marching cubes geometry may not merge as effectively
3. Additional processing time may not be worth it for small chunks
4. Current implementation preserves original triangles in merged sets (conservative approach)

## Testing
To test the optimization:
1. Create terrain with flat surfaces using the terrain modifier
2. Compare vertex count with greedy meshing on vs off
3. Monitor frame rate and chunk generation time
4. Adjust based on your specific use case

## Integration with Existing Code
The implementation is backward compatible:
- Setting `_useGreedyMeshing = false` uses the original marching cubes output
- All existing terrain modification and chunk management continues to work unchanged
- The Chunk.cs, ChunkManager.cs, and other systems require no modifications
