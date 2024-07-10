using UnityEngine;

public class GridManager : MonoBehaviour
{
    public GameObject cellPrefab;
    public GameObject wallPrefab;
    public int gridWidth = 10;
    public int gridHeight = 10;
    public float cellSpacing = 1.0f;

    void Start()
    {
        GenerateGrid();
    }

    void GenerateGrid()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                Vector3 position = new Vector3(x * cellSpacing, 0, z * cellSpacing);
                Instantiate(cellPrefab, position, Quaternion.identity, transform);
            }
        }
    }
}
