using UnityEngine;
using UnityEngine.UI;

public class CameraTerrainModifier : MonoBehaviour
{
    public Text _textSize;
    public Text _textMaterial;

    [Tooltip("Range where the player can interact with the terrain")]
    public float _rangeHit = 100;

    [Tooltip("Force of modifications applied to the terrain")]
    public float _modiferStrengh = 10;

    [Tooltip("Size of the brush, number of vertex modified")]
    public float _sizeHit = 6;

    [Tooltip("Color of the new voxels generated")] [Range(0, Constants.NUMBER_MATERIALS - 1)]
    public int _buildingMaterial;

    private RaycastHit hit;
    private ChunkManager chunkManager;

    void Awake()
    {
        chunkManager = ChunkManager.Instance;
        UpdateUI();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            float modification = (Input.GetMouseButton(0)) ? _modiferStrengh : -_modiferStrengh;
            if (Physics.Raycast(transform.position, transform.forward, out hit, _rangeHit))
            {
                chunkManager.ModifyChunkData(hit.point, _sizeHit, modification, _buildingMaterial);
            }
        }

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
        _textSize.text = "(+ -) Brush size: " + _sizeHit;
        _textMaterial.text = "(Mouse wheel) Actual material: " + _buildingMaterial;
    }
}