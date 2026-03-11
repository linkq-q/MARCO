using UnityEngine;

[CreateAssetMenu(
    fileName = "SkyColorConfig",
    menuName = "Config/Sky Color Config"
)]
public class SkyColorConfig : ScriptableObject
{
    public Color skyColor = Color.cyan;
    public Color horizonColor = Color.white;
    public Color groundColor = Color.gray;
}
