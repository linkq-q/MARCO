using UnityEngine;

[CreateAssetMenu(menuName = "Ending/Fixed Ending Asset", fileName = "Ending_Fixed")]
public class FixedEndingAsset : ScriptableObject
{
    [Header("Meta")]
    public string endingId = "lost";
    public string title = "迷失结局";

    [Header("Body (will be split by lines)")]
    [TextArea(10, 80)]
    public string bodyText;

    [Header("Typewriter Override (optional)")]
    [Tooltip("<=0 表示不覆盖，使用打字机脚本自身参数")]
    public float charIntervalOverride = -1f;

    [Tooltip("<=0 表示不覆盖，使用打字机脚本自身参数")]
    public float linePauseOverride = -1f;
}