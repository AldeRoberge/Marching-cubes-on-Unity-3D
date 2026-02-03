using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraTerrainModifier : MonoBehaviour
{
    public Text _textSize;
    public Text _textMaterial;

    [Tooltip("Range where the player can interact with the terrain")]
    public float _rangeHit = 100;

    [Tooltip("Color of the new voxels generated")]
    [Range(0, Constants.NUMBER_MATERIALS - 1)]
    public int _buildingMaterial;

    [Tooltip("Sensitivity of the mouse drag")]
    public float _sensitivity = 1.0f;

    [Tooltip("Density change per unit of mouse movement")]
    public float _densityChangePerUnit = 50f;

    // Terraforming modes

    public enum TerraformMode { Standard } // Simplified for RCT style
    public enum SelectionMode { Face, Corner }

    private TerraformMode _currentMode = TerraformMode.Standard;
    private SelectionMode _currentSelectionMode = SelectionMode.Face;

    // Drag state
    private bool _isDragging;
    private float _accumulatedDrag; // Accumulates raw mouse input
    private float _appliedAccumulator; // Tracks applied modifications to handle remainders

    // Selection state


    private List<Vector3> _lockedSelectedPoints = new List<Vector3>();
    private Vector3 _lockedHitCenter;
    private bool _hasLockedSelection;

    private RaycastHit hit;
    private ChunkManager chunkManager;
    private FlyingCamera _flyingCamera;

    // Gizmo visualization
    private List<Vector3> _currentPreviewPoints = new List<Vector3>();
    private Vector3 _currentPreviewCenter;
    private bool _hasCurrentHit;

    void Awake()
    {
        chunkManager = ChunkManager.Instance;
        _flyingCamera = GetComponent<FlyingCamera>();
        if (_flyingCamera == null) _flyingCamera = Camera.main.GetComponent<FlyingCamera>();
        if (_flyingCamera == null) _flyingCamera = FindObjectOfType<FlyingCamera>();


        UpdateUI();
        Debug.Log("CameraTerrainModifier initialized (RCT Style)");
    }

    void Update()
    {
        // Terraforming drag system
        HandleTerraformingDrag();

        // Update gizmo preview
        UpdateGizmoPreview();

        //Inputs
        if (Input.GetAxis("Mouse ScrollWheel") > 0 && _buildingMaterial != Constants.NUMBER_MATERIALS - 1)
        {
            _buildingMaterial++;
            UpdateUI();
        }
        else if (Input.GetAxis("Mouse ScrollWheel") < 0 && _buildingMaterial != 0)
        {
            _buildingMaterial--;
            UpdateUI();
        }
    }

    public void UpdateUI()
    {
        _textSize.text = "RCT Mode | Auto-Selection (Face/Corner)";
        _textMaterial.text = "(Mouse wheel) Material: " + _buildingMaterial + " | (Left Mouse) Drag to Raise/Lower";
    }

    private void DetermineSelection(Vector3 hitPoint)
    {
        Vector3 localPos = hitPoint / Constants.VOXEL_SIDE;
        Vector3 rounded = new Vector3(Mathf.Round(localPos.x), Mathf.Round(localPos.y), Mathf.Round(localPos.z));
        Vector3 diff = localPos - rounded;

        // Threshold to determine if we are looking at a corner or the middle of a face
        // If close to x-integer AND z-integer (edges), it's a corner.
        float cornerThreshold = 0.25f;


        _currentPreviewPoints.Clear();
        _currentPreviewCenter = itemSpaceToWorld(rounded); // Use rounded as center for corner, adjusted for face later

        // Check if we are aiming at a corner (both X and Z are close to integers)
        // Checking X and Z only because we usually edit the 'ground' (Y). 
        // But for true 3D, we might check all. Assuming Top-Down/Angle view on "Ground".
        if (Mathf.Abs(diff.x) < cornerThreshold && Mathf.Abs(diff.z) < cornerThreshold)
        {
            _currentSelectionMode = SelectionMode.Corner;
            _currentPreviewPoints.Add(itemSpaceToWorld(rounded));
            _currentPreviewCenter = itemSpaceToWorld(rounded);
        }
        else
        {
            _currentSelectionMode = SelectionMode.Face;

            // Identify the face (Cell)
            // We want the floor of the position to find the bottom-left corner of the cell

            int x = Mathf.FloorToInt(localPos.x);
            int z = Mathf.FloorToInt(localPos.z);
            int y = Mathf.RoundToInt(localPos.y); // Use aims Y level

            // Face (Quad) corners
            _currentPreviewPoints.Add(itemSpaceToWorld(new Vector3(x, y, z)));
            _currentPreviewPoints.Add(itemSpaceToWorld(new Vector3(x + 1, y, z)));
            _currentPreviewPoints.Add(itemSpaceToWorld(new Vector3(x, y, z + 1)));
            _currentPreviewPoints.Add(itemSpaceToWorld(new Vector3(x + 1, y, z + 1)));


            _currentPreviewCenter = itemSpaceToWorld(new Vector3(x + 0.5f, y, z + 0.5f));
        }
    }

    private Vector3 itemSpaceToWorld(Vector3 itemSpace)
    {
        return itemSpace * Constants.VOXEL_SIDE;
    }

    private void UpdateGizmoPreview()
    {
        if (!_isDragging)
        {
            if (Physics.Raycast(transform.position, transform.forward, out hit, _rangeHit))
            {
                DetermineSelection(hit.point);
                _hasCurrentHit = true;
            }
            else
            {
                _hasCurrentHit = false;
            }
        }
    }

    private void HandleTerraformingDrag()
    {
        // Detect drag start
        if (Input.GetMouseButtonDown(0))
        {
            if (Physics.Raycast(transform.position, transform.forward, out hit, _rangeHit))
            {
                _isDragging = true;
                if (_flyingCamera != null) _flyingCamera.InputLocked = true;


                _accumulatedDrag = 0f;
                _appliedAccumulator = 0f;

                // LOCK the current selection

                DetermineSelection(hit.point);
                _lockedSelectedPoints = new List<Vector3>(_currentPreviewPoints);
                _lockedHitCenter = _currentPreviewCenter;
                _hasLockedSelection = true;
            }
        }

        // Detect Right Click (Remove Voxel)
        if (Input.GetMouseButtonDown(1))
        {
            if (Physics.Raycast(transform.position, transform.forward, out hit, _rangeHit))
            {
                DetermineSelection(hit.point);
                // Apply large negative modification to remove
                Debug.Log($"[TERRAFORM RCT] Right Click: Removing {_currentPreviewPoints.Count} points");
                chunkManager.ModifyDiscretePoints(_currentPreviewPoints, -255, 0);
            }
        }

        // Processing Drag
        if (_isDragging && Input.GetMouseButton(0))
        {
            // Use Input.GetAxis for delta logic to support locked cursor

            float inputDelta = Input.GetAxis("Mouse Y");

            // Accumulate input based on sensitivity

            _accumulatedDrag += inputDelta * _sensitivity;

            // Calculate total modification that SHOULD exist based on accumulated drag
            // 1 unit of drag = _densityChangePerUnit density change

            float targetModification = _accumulatedDrag * _densityChangePerUnit;

            // Calculate how much we have already applied
            // We want to apply the difference between target and what we've already done

            float modificationDelta = targetModification - _appliedAccumulator;

            // Only apply if we have at least +/- 1 unit of integer change

            int amountToApply = (int)modificationDelta;


            if (amountToApply != 0)
            {
                ApplyTerraformOperation(amountToApply);
                _appliedAccumulator += amountToApply; // Track what we physically applied
            }
        }

        // Detect drag end
        if (Input.GetMouseButtonUp(0))
        {
            _isDragging = false;
            if (_flyingCamera != null) _flyingCamera.InputLocked = false;
            _hasLockedSelection = false;
        }
    }

    private void ApplyTerraformOperation(int modificationAmount)
    {
        if (!_hasLockedSelection || _lockedSelectedPoints.Count == 0) return;

        Debug.Log($"[TERRAFORM RCT] Applying {modificationAmount} to {_lockedSelectedPoints.Count} points.");


        chunkManager.ModifyDiscretePoints(_lockedSelectedPoints, modificationAmount, _buildingMaterial);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // Draw Preview (Green)
        if (_hasCurrentHit && !_isDragging)
        {
            Gizmos.color = Color.green;
            DrawSelectionGizmos(_currentPreviewPoints, _currentPreviewCenter);
        }

        // Draw Locked Selection (Cyan)
        if (_hasLockedSelection && _isDragging)
        {
            Gizmos.color = Color.cyan;
            DrawSelectionGizmos(_lockedSelectedPoints, _lockedHitCenter);

            // Draw height indicator

            Gizmos.color = Color.yellow;
            // Visual scale: show accumulated drag
            Vector3 targetPos = _lockedHitCenter + Vector3.up * (_accumulatedDrag * 0.5f);

            Gizmos.DrawLine(_lockedHitCenter, targetPos);
            Gizmos.DrawSphere(targetPos, 0.2f);
        }
    }

    private void DrawSelectionGizmos(List<Vector3> points, Vector3 center)
    {
        if (points.Count == 1)
        {
            // Corner
            Gizmos.DrawWireSphere(points[0], 0.2f);
            Gizmos.DrawCube(points[0], Vector3.one * 0.1f);
        }
        else if (points.Count == 4)
        {
            // Face - Draw Quad
            // Assuming order: 00, 10, 01, 11 (x,z)
            // But List order from DetermineSelection is: (x,y,z), (x+1,y,z), (x,y,z+1), (x+1,y,z+1)
            // 0: (0,0)
            // 1: (1,0)
            // 2: (0,1)
            // 3: (1,1)
            // Lines: 0-1, 1-3, 3-2, 2-0


            Gizmos.DrawLine(points[0], points[1]);
            Gizmos.DrawLine(points[1], points[3]);
            Gizmos.DrawLine(points[3], points[2]);
            Gizmos.DrawLine(points[2], points[0]);

            // Draw Center

            Gizmos.DrawSphere(center, 0.1f);
        }
    }
#endif
}
