using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEditor;
using System.Collections.Generic;

public enum ColorType
{
    Red,
    Blue,
    Yellow,
    Green,
    Purple
}

[System.Serializable]
public class ShooterConfig
{
    public ColorType color;
    public int shots = 20;
}

[CreateAssetMenu(fileName = "LevelData", menuName = "ThisIsBlast/Level Data")]
public class LevelData : SerializedScriptableObject
{
    [Title("Blocks Grid", TitleAlignment = TitleAlignments.Centered, Bold = true)]
    [TableMatrix(SquareCells = true, DrawElementMethod = "DrawBlockCell", ResizableColumns = true, HideColumnIndices = true, HideRowIndices = true)]
    public ColorType[,] BlocksGrid = new ColorType[8, 6];

    [Title("Shooters Grid", TitleAlignment = TitleAlignments.Centered, Bold = true)]
    [TableMatrix(SquareCells = true, DrawElementMethod = "DrawShooterCell", ResizableColumns = true, HideColumnIndices = true, HideRowIndices = true)]
    public ShooterConfig[,] ShootersGrid = new ShooterConfig[4, 8];

    public int GridWidth => BlocksGrid != null ? BlocksGrid.GetLength(0) : 0;
    public int GridHeight => BlocksGrid != null ? BlocksGrid.GetLength(1) : 0;
    public int QueueGridWidth => ShootersGrid != null ? ShootersGrid.GetLength(0) : 0;
    public int QueueGridHeight => ShootersGrid != null ? ShootersGrid.GetLength(1) : 0;

    [FoldoutGroup("Generation Tools"), HorizontalGroup("Generation Tools/BlockGen")] public int GenWidth = 8, GenHeight = 6;
    [FoldoutGroup("Generation Tools"), HorizontalGroup("Generation Tools/QueueGen")] public int GenQueueWidth = 3, GenQueueHeight = 2;
    [FoldoutGroup("Generation Tools"), MinMaxSlider(1, 8, true)] public Vector2Int BatchWidthRange = new(2, 4), BatchHeightRange = new(2, 4);
    [FoldoutGroup("Generation Tools"), MinMaxSlider(1, 50, true)] public Vector2Int ShotsPerShooterRange = new Vector2Int(10, 25);

    [FoldoutGroup("Generation Tools"), Button(ButtonSizes.Medium)]
    private void GenerateBlocksGrid()
    {
        BlocksGrid = new ColorType[GenWidth, GenHeight];
        int cellsFilled = 0;
        int maxCells = GenWidth * GenHeight;

        while (cellsFilled < maxCells)
        {
            ColorType batchColor = (ColorType)Random.Range(0, 5);

            // Use the new Batch Range variables for clumpy generation
            int batchW = Random.Range(BatchWidthRange.x, BatchWidthRange.y + 1);
            int batchH = Random.Range(BatchHeightRange.x, BatchHeightRange.y + 1);

            int startX = Random.Range(0, GenWidth);
            int startY = Random.Range(0, GenHeight);

            for (int x = startX; x < startX + batchW && x < GenWidth; x++)
            {
                for (int y = startY; y < startY + batchH && y < GenHeight; y++)
                {
                    BlocksGrid[x, y] = batchColor;
                }
            }
            cellsFilled += batchW * batchH;
        }
    }

    [FoldoutGroup("Generation Tools"), Button(ButtonSizes.Medium)]
    private void GenerateShootersQueue()
    {
        if (BlocksGrid == null || BlocksGrid.Length == 0)
        {
            Debug.LogWarning("Please generate the Blocks Grid first!");
            return;
        }

        // Step 1: Count exact blocks on the board
        Dictionary<ColorType, int> requiredCounts = new Dictionary<ColorType, int>();
        foreach (ColorType color in System.Enum.GetValues(typeof(ColorType)))
        {
            requiredCounts[color] = 0;
        }

        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                requiredCounts[BlocksGrid[x, y]]++;
            }
        }

        // Step 2: Create a Weighted Pool for proportional distribution
        List<ColorType> activeColors = new List<ColorType>();
        List<ColorType> weightedPool = new List<ColorType>();

        foreach (var kvp in requiredCounts)
        {
            if (kvp.Value > 0)
            {
                activeColors.Add(kvp.Key);

                // Add the color to the pool multiple times based on its block count.
                // 20 Red blocks = 20 Red entries in the pool. 4 Purple = 4 Purple entries.
                for (int i = 0; i < kvp.Value; i++)
                {
                    weightedPool.Add(kvp.Key);
                }
            }
        }

        int totalSlots = GenQueueWidth * GenQueueHeight;
        if (activeColors.Count > totalSlots)
        {
            Debug.LogWarning($"Queue needs at least {activeColors.Count} slots to hold all active colors!");
            return;
        }

        if (weightedPool.Count == 0)
        {
            Debug.LogWarning("The Blocks Grid is completely empty!");
            return;
        }

        // Step 3: Assign queue slots proportionally
        List<ColorType> slotColors = new List<ColorType>(activeColors); // Guarantee 1 of each required color
        while (slotColors.Count < totalSlots)
        {
            // Pick a random entry from the weighted pool. 
            // Colors with huge block counts will naturally claim the majority of the extra shooters.
            slotColors.Add(weightedPool[Random.Range(0, weightedPool.Count)]);
        }

        // Step 4: Assign random, LARGE shot counts to create the "Buffer"
        List<ShooterConfig> generatedShooters = new List<ShooterConfig>();
        Dictionary<ColorType, int> generatedTotals = new Dictionary<ColorType, int>();

        foreach (var color in activeColors) generatedTotals[color] = 0;

        foreach (var color in slotColors)
        {
            int randomShots = Random.Range(ShotsPerShooterRange.x, ShotsPerShooterRange.y + 1);
            generatedShooters.Add(new ShooterConfig { color = color, shots = randomShots });
            generatedTotals[color] += randomShots;
        }

        // Step 5: Verification - Ensure we didn't accidentally roll under the required amount
        foreach (var color in activeColors)
        {
            if (generatedTotals[color] < requiredCounts[color])
            {
                ShooterConfig shooterToBoost = generatedShooters.Find(s => s.color == color);
                int deficit = requiredCounts[color] - generatedTotals[color];
                shooterToBoost.shots += deficit;
            }
        }

        // Step 6: Shuffle the queue
        for (int i = 0; i < generatedShooters.Count; i++)
        {
            int randomIndex = Random.Range(i, generatedShooters.Count);
            (generatedShooters[i], generatedShooters[randomIndex]) = (generatedShooters[randomIndex], generatedShooters[i]);
        }

        // Step 7: Map back to the 2D grid
        ShootersGrid = new ShooterConfig[GenQueueWidth, GenQueueHeight];
        int listIndex = 0;
        for (int row = 0; row < GenQueueHeight; row++)
        {
            for (int col = 0; col < GenQueueWidth; col++)
            {
                ShootersGrid[col, row] = generatedShooters[listIndex];
                listIndex++;
            }
        }
    }

#if UNITY_EDITOR
    private static ColorType DrawBlockCell(Rect rect, ColorType value)
    {
        EditorGUI.DrawRect(rect.Padding(2), GetColor(value));
        var whiteLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { normal = { textColor = Color.white }, hover = { textColor = Color.black } };
        return (ColorType)EditorGUI.EnumPopup(rect, value, whiteLabel);
    }

    private static ShooterConfig DrawShooterCell(Rect rect, ShooterConfig value)
    {
        if (value == null) value = new ShooterConfig();

        // Draw Background Color
        EditorGUI.DrawRect(rect.Padding(2), GetColor(value.color));

        // Divide the rect so we can edit Color AND Count
        Rect colorRect = new Rect(rect.x, rect.y, rect.width, rect.height * 0.8f);
        Rect countRect = new Rect(rect.x, rect.y + rect.height * 0.8f, rect.width, rect.height * 0.2f);

        // Draw UI Elements
        var whiteLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { normal = { textColor = Color.white }, hover = { textColor = Color.black } };
        value.color = (ColorType)EditorGUI.EnumPopup(colorRect, value.color, whiteLabel);
        value.shots = EditorGUI.IntField(countRect, value.shots);

        return value;
    }

    private static Color GetColor(ColorType type)
    {
        return type switch
        {
            ColorType.Red => new Color(1f, 0.4f, 0.4f),
            ColorType.Blue => new Color(0.4f, 0.6f, 1f),
            ColorType.Yellow => new Color(1f, 0.9f, 0.2f),
            ColorType.Green => new Color(0.4f, 1f, 0.4f),
            ColorType.Purple => new Color(0.8f, 0.4f, 1f),
            _ => Color.white
        };
    }
#endif
}