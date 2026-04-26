using TUTORIAL_SYSTEM;
using UnityEngine;

public class ShooterQueueGrid : MonoBehaviour
{
    [SerializeField] private Grid grid;
    [SerializeField] private ShooterEntity shooterEntityPrefab;
    [SerializeField] private Transform gridRoot;

    private ShooterEntity[,] _grid;
    private int _width;
    private int _height;
    private Vector3 _horizontalOffset;

    public int TotalRemaining { get; private set; }
    public bool IsEmpty => TotalRemaining <= 0;

    public void BuildGrid(LevelData data)
    {
        DestroyAllEntities();

        _width = data.QueueGridWidth;
        _height = data.QueueGridHeight;
        _grid = new ShooterEntity[_width, _height];
        TotalRemaining = 0;

        // Calculate Horizontal Centering Offset
        // We only center the X-axis so the FRONT row (row 0) stays at the local Z-start point.
        Vector3 firstCellX = grid.GetCellCenterLocal(new Vector3Int(0, 0, 0));
        Vector3 lastCellX = grid.GetCellCenterLocal(new Vector3Int(_width - 1, 0, 0));
        _horizontalOffset = new Vector3((firstCellX.x + lastCellX.x) * 0.5f, 0, 0);

        for (int row = 0; row < _height; row++)
        {
            for (int col = 0; col < _width; col++)
            {
                ShooterConfig config = data.ShootersGrid[col, row];

                Vector3 localPos = GetLocalCellPosition(col, row);

                ShooterEntity entity = Instantiate(shooterEntityPrefab, gridRoot);
                entity.transform.SetLocalPositionAndRotation(localPos, Quaternion.identity);
                entity.Initialize(config, GameManager.Instance.GetColor(config.color), col, row);

                _grid[col, row] = entity;
                TotalRemaining++;
            }
        }

        if (!TutorialManager.Instance.AllTutorialsCompleted)
        {
            if (GameManager.Instance.CurrentLevelIndex == 0)
            {
                var stage = TutorialManager.Instance.Tutorials[0].Stages[0];
                var hand = stage.MyModules[0] as TutorialModule_DynamicHand;
                var cutout = stage.MyModules[1] as TutorialModule_CutOutMask;
                var entity = _grid[0, 0].transform;

                hand?.Points.Add(new()
                {
                    Point = entity,
                    Offset = new(0.3f, 0f, -0.25f)
                });
                cutout.Target = entity;

                stage.StageStarted();
            }
            else if (GameManager.Instance.CurrentLevelIndex == 3)
            {
                var stage = TutorialManager.Instance.Tutorials[0].Stages[1];
                var cutout = stage.MyModules[1] as TutorialModule_CutOutMask;
                var entity = _grid[1, 0].transform;

                cutout.Target = entity;

                stage.StageStarted();
            }
        }

        RefreshFrontRowInteractability();
    }

    /// Calculates the local position including the dynamic horizontal centering.
    private Vector3 GetLocalCellPosition(int col, int row)
    {
        // Use -row so row 1 is behind row 0
        Vector3 gridPos = grid.GetCellCenterLocal(new Vector3Int(col, -row, 0));
        return gridPos - _horizontalOffset;
    }

    public Vector3 GetWorldPosition(int col, int row)
    {
        Vector3 local = GetLocalCellPosition(col, row);
        return gridRoot != null ? gridRoot.TransformPoint(local) : local;
    }

    public void NotifyEntityTakenFromFrontRow(int col)
    {
        if (_grid == null) return;

        _grid[col, 0] = null;
        TotalRemaining = Mathf.Max(0, TotalRemaining - 1);

        ShiftColumnForward(col);
        RefreshFrontRowInteractability();

        if (!TutorialManager.Instance.AllTutorialsCompleted)
        {
            if (GameManager.Instance.CurrentLevelIndex == 0)
            {
                var stage = TutorialManager.Instance.Tutorials[0].Stages[0];
                stage.StageCompleted();
            }
            else if (GameManager.Instance.CurrentLevelIndex == 3)
            {
                var stage = TutorialManager.Instance.Tutorials[0].Stages[1];
                stage.StageCompleted();
            }
        }
    }

    private void ShiftColumnForward(int col)
    {
        for (int row = 0; row < _height - 1; row++)
        {
            _grid[col, row] = _grid[col, row + 1];
            _grid[col, row + 1] = null;

            if (_grid[col, row] != null)
            {
                // Move forward to the next row's world position
                _grid[col, row].SetGridRow(row);
            }
        }
    }

    private void RefreshFrontRowInteractability()
    {
        if (_grid == null) return;

        for (int row = 0; row < _height; row++)
        {
            for (int col = 0; col < _width; col++)
            {
                ShooterEntity entity = _grid[col, row];
                if (entity != null)
                    entity.SetInteractable(row == 0);
            }
        }
    }

    private void DestroyAllEntities()
    {
        if (gridRoot != null)
        {
            foreach (Transform child in gridRoot)
                Destroy(child.gameObject);
        }
        _grid = null;
        TotalRemaining = 0;
    }
}