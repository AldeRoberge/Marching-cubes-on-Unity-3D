using UnityEngine;
using UnityEngine.UI;

public class CameraTerrainModifier : MonoBehaviour
{
    public Text _textSize;
    public Text _textMaterial;

    [Tooltip("Range where the player can interact with the terrain")]
    public float _rangeHit = 100;

    [Tooltip("Size of the brush, number of vertex modified")]
    public float _sizeHit = 6;

    [Tooltip("Color of the new voxels generated")] [Range(0, Constants.NUMBER_MATERIALS - 1)]
    public int _buildingMaterial;

    [Tooltip("Pixels of vertical mouse movement per terrain height step")]
    public float _pixelsPerHeightStep = 20f;

    [Tooltip("Height change per step when dragging (increase for more visible effect)")]
    public float _heightStepSize = 15f; // INCREASED from 5 to 15 for more visible effect

    // Terraforming modes
    public enum TerraformMode { Smooth, Cliff }
    public enum SelectionType { Area, SingleCorner }

    private TerraformMode _currentMode = TerraformMode.Smooth;
    private SelectionType _currentSelection = SelectionType.Area;

    // Drag state
    private bool _isDragging;
    private float _dragStartMouseY;
    private float _accumulatedMouseDelta;
    private int _currentHeightStep;
    private Vector3 _lockedHitPoint;
    private Vector3 _lockedHitNormal;
    private bool _hasLockedSelection;

    private RaycastHit hit;
    private ChunkManager chunkManager;

    // Gizmo visualization
    private Vector3 _currentHitPoint;
    private bool _hasCurrentHit;

    void Awake()
    {
        chunkManager = ChunkManager.Instance;
        UpdateUI();
        Debug.Log("CameraTerrainModifier initialized");
    }

    // Update is called once per frame
    void Update()
    {
        // Handle terraforming mode toggle (T key)
        if (Input.GetKeyDown(KeyCode.T))
        {
            _currentMode = (_currentMode == TerraformMode.Smooth) ? TerraformMode.Cliff : TerraformMode.Smooth;
            UpdateUI();
            Debug.Log($"Mode switched to: {_currentMode}");
        }

        // Handle selection type toggle (G key)
        if (Input.GetKeyDown(KeyCode.G))
        {
            _currentSelection = (_currentSelection == SelectionType.Area) ? SelectionType.SingleCorner : SelectionType.Area;
            UpdateUI();
            Debug.Log($"Selection switched to: {_currentSelection}");
        }

        // Terraforming drag system (LEFT mouse button - changed from middle)
        HandleTerraformingDrag();

        // Update gizmo preview (raycast every frame for visual feedback)
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

        if (Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus))
        {
            _sizeHit++;
            UpdateUI();
        }
        else if ((Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) && _sizeHit > 1)
        {
            _sizeHit--;
            UpdateUI();
        }
    }

    public void UpdateUI()
    {
        _textSize.text = "(+ -) Brush size: " + _sizeHit + " | (T) Mode: " + _currentMode + " | (G) Selection: " + _currentSelection;
        _textMaterial.text = "(Mouse wheel) Actual material: " + _buildingMaterial + " | (Left Mouse) Drag to terraform";
    }

    /// <summary>
    /// Update the gizmo preview showing where the user is aiming
    /// </summary>
    private void UpdateGizmoPreview()
    {
        // Only show preview when not dragging
        if (!_isDragging)
        {
            if (Physics.Raycast(transform.position, transform.forward, out hit, _rangeHit))
            {
                _currentHitPoint = hit.point;
                _hasCurrentHit = true;
            }
            else
            {
                _hasCurrentHit = false;
            }
        }
    }

    /// <summary>
    /// Handle the drag-based terraforming system
    /// Track mouse press, drag movement, and convert to height steps
    /// </summary>
    private void HandleTerraformingDrag()
    {
        // Detect drag start (LEFT mouse button press - changed from button 2 to button 0)
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log($"[TERRAFORM] Left mouse button DOWN - Starting raycast from {transform.position} forward {transform.forward}");
            
            // Lock the terrain selection
            if (Physics.Raycast(transform.position, transform.forward, out hit, _rangeHit))
            {
                _isDragging = true;
                _dragStartMouseY = Input.mousePosition.y;
                _accumulatedMouseDelta = 0f;
                _currentHeightStep = 0;
                _lockedHitPoint = hit.point;
                _lockedHitNormal = hit.normal;
                _hasLockedSelection = true;

                Debug.Log($"[TERRAFORM] ✓ DRAG STARTED | Hit: {_lockedHitPoint} | Normal: {_lockedHitNormal} | Mouse Y: {_dragStartMouseY}");
                Debug.Log($"[TERRAFORM] Hit Collider: {hit.collider.name} | Distance: {hit.distance}");
            }
            else
            {
                Debug.LogWarning($"[TERRAFORM] ✗ RAYCAST FAILED | Range: {_rangeHit} | From: {transform.position} | Direction: {transform.forward}");
            }
        }

        // Track vertical mouse movement during drag (LEFT mouse button - changed from button 2 to button 0)
        if (_isDragging && Input.GetMouseButton(0))
        {
            float currentMouseY = Input.mousePosition.y;
            float mouseDelta = currentMouseY - _dragStartMouseY;
            
            // Calculate height step from accumulated mouse movement
            int newHeightStep = Mathf.RoundToInt(mouseDelta / _pixelsPerHeightStep);
            
            // Clamp to terrain height limits
            int maxSteps = Mathf.FloorToInt(Constants.MAX_HEIGHT / 2 / _heightStepSize);
            newHeightStep = Mathf.Clamp(newHeightStep, -maxSteps, maxSteps);

            // Debug mouse movement every frame while dragging
            if (Time.frameCount % 30 == 0) // Log every 30 frames to avoid spam
            {
                Debug.Log($"[TERRAFORM] Dragging | Mouse Delta: {mouseDelta:F1}px | Current Step: {_currentHeightStep} | New Step: {newHeightStep}");
            }

            // Apply terrain modification if height step changed
            if (newHeightStep != _currentHeightStep && _hasLockedSelection)
            {
                int stepDifference = newHeightStep - _currentHeightStep;
                Debug.Log($"[TERRAFORM] ⚡ STEP CHANGE | Difference: {stepDifference} | Mouse Delta: {mouseDelta:F1}px");
                ApplyTerraformOperation(stepDifference);
                _currentHeightStep = newHeightStep;
            }
        }

        // Detect drag end (LEFT mouse button release - changed from button 2 to button 0)
        if (Input.GetMouseButtonUp(0))
        {
            if (_isDragging)
            {
                Debug.Log($"[TERRAFORM] ✓ DRAG ENDED | Total Steps: {_currentHeightStep} | Total Height Change: {_currentHeightStep * _heightStepSize}");
                _isDragging = false;
                _hasLockedSelection = false;
            }
        }
    }

    /// <summary>
    /// Apply the terrain modification based on current mode and selection type
    /// </summary>
    /// <param name="stepDifference">Number of height steps to apply (positive = raise, negative = lower)</param>
    private void ApplyTerraformOperation(int stepDifference)
    {
        if (!_hasLockedSelection)
        {
            Debug.LogWarning("[TERRAFORM] Cannot apply - no locked selection!");
            return;
        }

        float modification = stepDifference * _heightStepSize;
        
        Debug.Log($"[TERRAFORM] === APPLYING OPERATION ===");
        Debug.Log($"[TERRAFORM] Step Difference: {stepDifference}");
        Debug.Log($"[TERRAFORM] Height Step Size: {_heightStepSize}");
        Debug.Log($"[TERRAFORM] Calculated Modification: {modification}");
        Debug.Log($"[TERRAFORM] Position: {_lockedHitPoint}");
        Debug.Log($"[TERRAFORM] Brush Size: {_sizeHit}");
        Debug.Log($"[TERRAFORM] Material: {_buildingMaterial}");
        Debug.Log($"[TERRAFORM] Mode: {_currentMode}");
        Debug.Log($"[TERRAFORM] Selection: {_currentSelection}");

        if (_currentSelection == SelectionType.Area)
        {
            // Area-based modification
            if (_currentMode == TerraformMode.Smooth)
            {
                // Smooth mode: gradient falloff based on distance
                Debug.Log($"[TERRAFORM] Calling ModifyChunkData for SMOOTH mode");
                chunkManager.ModifyChunkData(_lockedHitPoint, _sizeHit, modification, _buildingMaterial);
            }
            else // Cliff mode
            {
                // Cliff mode: uniform height change across entire area
                // Apply stronger modification for cliff effect
                float cliffModification = modification * 3f; // INCREASED multiplier for cliff effect
                Debug.Log($"[TERRAFORM] Calling ModifyChunkData for CLIFF mode | Amplified Mod: {cliffModification}");
                chunkManager.ModifyChunkData(_lockedHitPoint, _sizeHit, cliffModification, _buildingMaterial);
            }
        }
        else // SingleCorner
        {
            // Single corner modification
            float singlePointRange = 0.5f;
            Debug.Log($"[TERRAFORM] Calling ModifyChunkData for SINGLE CORNER | Range: {singlePointRange}");
            chunkManager.ModifyChunkData(_lockedHitPoint, singlePointRange, modification, _buildingMaterial);
        }

        Debug.Log($"[TERRAFORM] === OPERATION COMPLETE ===\n");
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draw gizmos to visualize the terraforming selection
    /// </summary>
    void OnDrawGizmos()
    {
        // Draw preview when hovering (yellow/green)
        if (_hasCurrentHit && !_isDragging)
        {
            Gizmos.color = Color.green;
            
            if (_currentSelection == SelectionType.Area)
            {
                // Draw sphere for area selection
                Gizmos.DrawWireSphere(_currentHitPoint, _sizeHit * Constants.VOXEL_SIDE / 2);
                
                // Draw cross at center
                float crossSize = 0.5f;
                Gizmos.DrawLine(_currentHitPoint + Vector3.left * crossSize, _currentHitPoint + Vector3.right * crossSize);
                Gizmos.DrawLine(_currentHitPoint + Vector3.forward * crossSize, _currentHitPoint + Vector3.back * crossSize);
                Gizmos.DrawLine(_currentHitPoint + Vector3.up * crossSize, _currentHitPoint + Vector3.down * crossSize);
            }
            else // Single corner
            {
                // Draw small cube for single corner
                Gizmos.DrawWireCube(_currentHitPoint, Vector3.one * Constants.VOXEL_SIDE);
                Gizmos.DrawSphere(_currentHitPoint, 0.3f);
            }
        }

        // Draw locked selection when dragging (cyan/magenta)
        if (_hasLockedSelection && _isDragging)
        {
            // Color based on current mode
            Gizmos.color = _currentMode == TerraformMode.Smooth ? Color.cyan : Color.magenta;
            
            if (_currentSelection == SelectionType.Area)
            {
                // Draw filled sphere for locked area
                Gizmos.DrawWireSphere(_lockedHitPoint, _sizeHit * Constants.VOXEL_SIDE / 2);
                Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.3f);
                Gizmos.DrawSphere(_lockedHitPoint, _sizeHit * Constants.VOXEL_SIDE / 2);
                
                // Draw height indicator line
                Gizmos.color = _currentHeightStep > 0 ? Color.green : (_currentHeightStep < 0 ? Color.red : Color.yellow);
                float heightOffset = _currentHeightStep * _heightStepSize;
                Vector3 targetPoint = _lockedHitPoint + Vector3.up * heightOffset;
                Gizmos.DrawLine(_lockedHitPoint, targetPoint);
                Gizmos.DrawSphere(targetPoint, 0.5f);
                
                // Draw text-like visualization with lines showing height
                for (int i = 0; i <= Mathf.Abs(_currentHeightStep); i++)
                {
                    float t = i / (float)Mathf.Max(1, Mathf.Abs(_currentHeightStep));
                    Vector3 pos = Vector3.Lerp(_lockedHitPoint, targetPoint, t);
                    Gizmos.DrawWireCube(pos, Vector3.one * 0.5f);
                }
            }
            else // Single corner locked
            {
                Gizmos.DrawWireCube(_lockedHitPoint, Vector3.one * Constants.VOXEL_SIDE * 1.5f);
                Gizmos.DrawSphere(_lockedHitPoint, 0.5f);
                
                // Height indicator
                Gizmos.color = _currentHeightStep > 0 ? Color.green : (_currentHeightStep < 0 ? Color.red : Color.yellow);
                float heightOffset = _currentHeightStep * _heightStepSize;
                Vector3 targetPoint = _lockedHitPoint + Vector3.up * heightOffset;
                Gizmos.DrawLine(_lockedHitPoint, targetPoint);
                Gizmos.DrawSphere(targetPoint, 0.8f);
            }
        }
    }
#endif
}
