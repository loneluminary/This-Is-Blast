using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class GridManager : MonoSingleton<GridManager>
{
    [SerializeField] private Grid grid;
    [SerializeField] private GridBlock blockPrefab;
    [SerializeField] private Transform gridRoot;

    public System.Action OnGridShifted;

    private GridBlock[,] _grid;
    private int _gridWidth, _gridHeight;
    private int _initialBlockCount;
    private int _destroyedCount;
    private Vector3 _horizontalOffset;

    public void BuildGrid(LevelData data)
    {
        ClearGrid();

        _gridWidth = data.GridWidth;
        _gridHeight = data.GridHeight;
        _grid = new GridBlock[_gridWidth, _gridHeight];
        _destroyedCount = 0;
        _initialBlockCount = 0;

        // Calculate Horizontal Centering Offset
        // We only center the X-axis so the FRONT row (row 0) stays at the local Z-start point.
        Vector3 firstCellX = grid.GetCellCenterLocal(new Vector3Int(0, 0, 0));
        Vector3 lastCellX = grid.GetCellCenterLocal(new Vector3Int(_gridWidth - 1, 0, 0));
        _horizontalOffset = new Vector3((firstCellX.x + lastCellX.x) * 0.5f, 0, 0);

        var seq = DOTween.Sequence();

        for (int row = 0; row < _gridHeight; row++)
        {
            for (int col = 0; col < _gridWidth; col++)
            {
                ColorType colorType = data.BlocksGrid[col, row];

                Vector3Int cellIndex = new(col, row);
                Vector3 localPos = grid.GetCellCenterLocal(cellIndex) - _horizontalOffset;

                GridBlock block = Instantiate(blockPrefab, gridRoot);
                block.transform.SetLocalPositionAndRotation(localPos, Quaternion.identity);
                block.Initialize(col, row, colorType, GameManager.Instance.GetColor(colorType));

                _grid[col, row] = block;
                _initialBlockCount++;

                // Animate upto 6 rows for performence witch will be visible to the player
                if (row <= 5)
                {
                    var originalScale = block.transform.localScale;
                    block.transform.localScale = Vector3.zero;
                    seq.Join(block.transform.DOScale(originalScale, 0.15f).SetEase(Ease.OutSine));
                }
            }

            if (row < 5) seq.AppendInterval(0.05f); // stagger by row
        }

        seq.Play();
    }

    private void ClearGrid()
    {
        if (_grid == null) return;

        foreach (Transform child in gridRoot)
            Destroy(child.gameObject);

        _grid = null;
    }

    /// Returns only the FRONT row blocks of the given color.
    public List<GridBlock> GetFrontBlocksOfColor(ColorType color, bool filterTargeted = true)
    {
        var results = new List<GridBlock>();
        if (_grid == null) return results;

        for (int col = 0; col < _gridWidth; col++)
        {
            GridBlock block = _grid[col, 0];
            if (block && block.ColorType == color && !block.IsDestroyed)
            {
                if (filterTargeted && block.IsTargeted) continue;
                results.Add(block);
            }
        }

        return results;
    }

    /// Returns true if any front-row block of this color exists (not yet targeted).
    public bool HasFrontBlocksOfColor(ColorType color)
    {
        if (_grid == null) return false;

        for (int col = 0; col < _gridWidth; col++)
        {
            GridBlock block = _grid[col, 0];
            if (block && block.ColorType == color && !block.IsDestroyed)
                return true;
        }

        return false;
    }

    public void NotifyBlockDestroyed(GridBlock block)
    {
        if (_grid == null) return;

        int col = block.GridCol;
        int row = block.GridRow;

        if (col < 0 || col >= _gridWidth || row < 0 || row >= _gridHeight) return;

        _grid[col, row] = null;
        _destroyedCount++;

        // Front-row block removed → collapse the column toward the front
        if (row == 0) ShiftColumnForward(col);
    }

    /// Slides every block in <paramref name="col"/> forward by one row after the front block is destroyed.
    /// The data array is updated immediately; blocks animate to their new world positions.
    private void ShiftColumnForward(int col)
    {
        for (int row = 0; row < _gridHeight - 1; row++)
        {
            _grid[col, row] = _grid[col, row + 1];
            _grid[col, row + 1] = null;

            if (_grid[col, row] != null) _grid[col, row].SetGridRow(row);
        }

        OnGridShifted?.Invoke();
    }

    public bool AllBlocksCleared()
    {
        if (_grid == null) return true;

        foreach (GridBlock block in _grid)
        {
            if (block && !block.IsDestroyed) return false;
        }

        return true;
    }

    public float GetClearProgress()
    {
        if (_initialBlockCount == 0) return 1f;
        return (float)_destroyedCount / _initialBlockCount;
    }

    /// Returns all non-destroyed blocks in row 0 regardless of color.
    public List<GridBlock> GetEntireFrontRow()
    {
        var results = new List<GridBlock>();
        if (_grid == null) return results;

        for (int col = 0; col < _gridWidth; col++)
        {
            GridBlock block = _grid[col, 0];
            if (block != null && !block.IsDestroyed) results.Add(block);
        }

        return results;
    }

    public Vector3 GetCellWorldPosition(int col, int row)
    {
        Vector3 local = grid.GetCellCenterLocal(new Vector3Int(col, row)) - _horizontalOffset;
        return gridRoot.TransformPoint(local);
    }
}