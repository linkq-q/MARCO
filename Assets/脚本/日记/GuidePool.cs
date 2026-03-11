using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Diary/GuidePool")]
public class GuidePool : ScriptableObject
{
    public List<GuideLine> lines = new List<GuideLine>();
}
