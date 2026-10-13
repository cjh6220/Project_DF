using System.Collections.Generic;
using UnityEngine;

namespace Proto.Feel
{
    /// <summary>프리팹 이름 → 프리팹 (SkillVfx가 런타임에 찾는다). Editor/SkillVfxLibraryBuilder가 자동으로 채운다.</summary>
    public class SkillVfxLibrary : ScriptableObject
    {
        public List<string> Names = new List<string>();
        public List<GameObject> Prefabs = new List<GameObject>();
    }
}
